using Microsoft.Extensions.Logging.Abstractions;
using Scrat.Core.Exceptions;
using Scrat.Core.Models;
using Scrat.Exporters.Smb;
using Scrat.Exporters.Smb.Tests.TestDoubles;

namespace Scrat.Exporters.Smb.Tests;

public class SmbExporterTests
{
    private readonly FakeSmbWorld _world = new();

    private SmbExporter CreateExporter() =>
        new(new FakeSmbConnectionFactory(_world), NullLogger<SmbExporter>.Instance);

    [Fact]
    public async Task WriteAsync_writes_full_content_under_sanitized_name()
    {
        await CreateExporter().WriteAsync(new ExportData(new byte[] { 1, 2, 3 }), "a/b");

        Assert.Equal(new byte[] { 1, 2, 3 }, _world.Files["a_b"]);
        Assert.Equal(1, _world.ConnectCount);
        Assert.Equal(0, _world.OpenConnections);
    }

    [Fact]
    public async Task WriteChunkedAsync_writes_sequential_offset_slices()
    {
        var content = Enumerable.Range(0, 10).Select(i => (byte)i).ToArray();

        await CreateExporter().WriteChunkedAsync(new ExportData(content), "key", chunkSizeBytes: 4);

        Assert.Equal(content, _world.Files["key"]);
        Assert.Equal([(0L, 4), (4L, 4), (8L, 2)], _world.WriteCalls.Select(w => (w.Offset, w.Length)));
    }

    [Fact]
    public async Task Stream_chunks_assemble_the_file_over_one_connection()
    {
        var exporter = CreateExporter();

        await exporter.WriteStreamChunkAsync("key", new byte[] { 1, 2 }, isFirst: true, isLast: false);
        await exporter.WriteStreamChunkAsync("key", new byte[] { 3, 4 }, isFirst: false, isLast: false);
        await exporter.WriteStreamChunkAsync("key", new byte[] { 5 }, isFirst: false, isLast: true);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, _world.Files["key"]);
        Assert.Equal(1, _world.ConnectCount);
        Assert.Equal(0, _world.OpenConnections);
    }

    [Fact]
    public async Task Faulted_stream_session_resumes_at_committed_offset_on_retry()
    {
        var exporter = CreateExporter();
        await exporter.WriteStreamChunkAsync("key", new byte[] { 1, 2 }, isFirst: true, isLast: false);

        _world.FailNextWrite = new IOException("broken pipe");
        await Assert.ThrowsAsync<ExportException>(() =>
            exporter.WriteStreamChunkAsync("key", new byte[] { 3, 4 }, isFirst: false, isLast: false));

        // The resilience decorator would re-invoke the same call; the exporter must reconnect and resume.
        await exporter.WriteStreamChunkAsync("key", new byte[] { 3, 4 }, isFirst: false, isLast: false);
        await exporter.WriteStreamChunkAsync("key", new byte[] { 5 }, isFirst: false, isLast: true);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, _world.Files["key"]);
        Assert.Equal(2, _world.ConnectCount);
        Assert.Equal(1, _world.OpenExistingCount);
        Assert.Equal(0, _world.OpenConnections);
    }

    [Fact]
    public async Task Retried_first_chunk_recreates_the_file_from_scratch()
    {
        var exporter = CreateExporter();

        _world.FailNextWrite = new IOException("broken pipe");
        await Assert.ThrowsAsync<ExportException>(() =>
            exporter.WriteStreamChunkAsync("key", new byte[] { 1, 2 }, isFirst: true, isLast: false));

        await exporter.WriteStreamChunkAsync("key", new byte[] { 1, 2 }, isFirst: true, isLast: true);

        Assert.Equal(new byte[] { 1, 2 }, _world.Files["key"]);
        Assert.Equal(2, _world.CreateCount);
    }

    [Fact]
    public async Task Stream_chunk_without_session_is_non_retryable()
    {
        await Assert.ThrowsAsync<NonRetryableExportException>(() =>
            CreateExporter().WriteStreamChunkAsync("key", new byte[1], isFirst: false, isLast: false));
    }

    [Fact]
    public async Task Abort_disposes_the_session_and_is_idempotent()
    {
        var exporter = CreateExporter();
        await exporter.WriteStreamChunkAsync("key", new byte[] { 1 }, isFirst: true, isLast: false);

        await exporter.AbortStreamAsync("key");
        await exporter.AbortStreamAsync("key");

        Assert.Equal(0, _world.OpenConnections);
        await Assert.ThrowsAsync<NonRetryableExportException>(() =>
            exporter.WriteStreamChunkAsync("key", new byte[1], isFirst: false, isLast: false));
    }
}
