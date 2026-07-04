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
    public async Task Stream_chunks_assemble_the_file_over_one_connection()
    {
        var exporter = CreateExporter();

        await exporter.OpenAsync("key");
        await exporter.WriteChunkAsync("key", new byte[] { 1, 2 });
        await exporter.WriteChunkAsync("key", new byte[] { 3, 4 });
        await exporter.WriteChunkAsync("key", new byte[] { 5 });
        await exporter.CloseAsync("key");

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, _world.Files["key"]);
        Assert.Equal(1, _world.ConnectCount);
        Assert.Equal(0, _world.OpenConnections);
    }

    [Fact]
    public async Task Faulted_stream_session_resumes_at_committed_offset_on_retry()
    {
        var exporter = CreateExporter();
        await exporter.OpenAsync("key");
        await exporter.WriteChunkAsync("key", new byte[] { 1, 2 });

        _world.FailNextWrite = new IOException("broken pipe");
        await Assert.ThrowsAsync<ExportException>(() =>
            exporter.WriteChunkAsync("key", new byte[] { 3, 4 }));

        // The resilience decorator would re-invoke the same call; the exporter must reconnect and resume.
        await exporter.WriteChunkAsync("key", new byte[] { 3, 4 });
        await exporter.WriteChunkAsync("key", new byte[] { 5 });
        await exporter.CloseAsync("key");

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, _world.Files["key"]);
        Assert.Equal(2, _world.ConnectCount);
        Assert.Equal(1, _world.OpenExistingCount);
        Assert.Equal(0, _world.OpenConnections);
    }

    [Fact]
    public async Task Partial_write_mid_chunk_is_rewritten_from_the_committed_offset_on_retry()
    {
        var exporter = CreateExporter();
        await exporter.OpenAsync("key");
        await exporter.WriteChunkAsync("key", new byte[] { 1, 2 });

        // The write commits 1 of the 2 bytes, then faults — the committed offset must not advance.
        _world.FailNextWriteAfterBytes = 1;
        await Assert.ThrowsAsync<ExportException>(() =>
            exporter.WriteChunkAsync("key", new byte[] { 3, 4 }));

        // Retry rewrites the same region from offset 2 (SMB writes are offset-addressed), no corruption.
        await exporter.WriteChunkAsync("key", new byte[] { 3, 4 });
        await exporter.CloseAsync("key");

        Assert.Equal(new byte[] { 1, 2, 3, 4 }, _world.Files["key"]);
        Assert.Equal(1, _world.OpenExistingCount);
        Assert.Equal(0, _world.OpenConnections);
    }

    [Fact]
    public async Task Retried_open_recreates_the_file_from_scratch()
    {
        var exporter = CreateExporter();

        // The resilience decorator would re-invoke Open; each open drops the stale session and recreates the file.
        await exporter.OpenAsync("key");
        await exporter.OpenAsync("key");
        await exporter.WriteChunkAsync("key", new byte[] { 1, 2 });
        await exporter.CloseAsync("key");

        Assert.Equal(new byte[] { 1, 2 }, _world.Files["key"]);
        Assert.Equal(2, _world.CreateCount);
        Assert.Equal(0, _world.OpenConnections);
    }

    [Fact]
    public async Task Chunk_without_session_is_non_retryable()
    {
        await Assert.ThrowsAsync<NonRetryableExportException>(() =>
            CreateExporter().WriteChunkAsync("key", new byte[1]));
    }

    [Fact]
    public async Task Abort_disposes_the_session_and_is_idempotent()
    {
        var exporter = CreateExporter();
        await exporter.OpenAsync("key");
        await exporter.WriteChunkAsync("key", new byte[] { 1 });

        await exporter.AbortStreamAsync("key");
        await exporter.AbortStreamAsync("key");

        Assert.Equal(0, _world.OpenConnections);
        await Assert.ThrowsAsync<NonRetryableExportException>(() =>
            exporter.WriteChunkAsync("key", new byte[1]));
    }
}
