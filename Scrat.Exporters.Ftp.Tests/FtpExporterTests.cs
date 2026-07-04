using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Scrat.Core.Exceptions;
using Scrat.Core.Models;
using Scrat.Exporters.Ftp;
using Scrat.Exporters.Ftp.Tests.TestDoubles;

namespace Scrat.Exporters.Ftp.Tests;

public class FtpExporterTests
{
    private readonly FakeFtpWorld _world = new();

    private FtpExporter CreateExporter() => new(
        new FakeFtpConnectionFactory(_world),
        Options.Create(new FtpOptions { BasePath = "/exports/" }),
        NullLogger<FtpExporter>.Instance);

    [Fact]
    public async Task WriteAsync_writes_full_content_under_base_path_with_sanitized_name()
    {
        await CreateExporter().WriteAsync(new ExportData(new byte[] { 1, 2, 3 }), "a/b");

        Assert.Equal(new byte[] { 1, 2, 3 }, _world.Files["/exports/a_b"]);
        Assert.Equal(1, _world.ConnectCount);
        Assert.Equal(0, _world.OpenConnections);
    }

    [Fact]
    public async Task WriteChunkedAsync_writes_all_slices_sequentially()
    {
        var content = Enumerable.Range(0, 10).Select(i => (byte)i).ToArray();

        await CreateExporter().WriteChunkedAsync(new ExportData(content), "key", chunkSizeBytes: 3);

        Assert.Equal(content, _world.Files["/exports/key"]);
    }

    [Fact]
    public async Task Stream_chunks_assemble_the_file_over_one_connection()
    {
        var exporter = CreateExporter();

        await exporter.WriteStreamChunkAsync("key", new byte[] { 1, 2 }, isFirst: true, isLast: false);
        await exporter.WriteStreamChunkAsync("key", new byte[] { 3, 4 }, isFirst: false, isLast: false);
        await exporter.WriteStreamChunkAsync("key", new byte[] { 5 }, isFirst: false, isLast: true);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, _world.Files["/exports/key"]);
        Assert.Equal(1, _world.ConnectCount);
        Assert.Equal(0, _world.OpenConnections);
    }

    [Fact]
    public async Task Faulted_session_resumes_by_appending_when_remote_size_matches_committed_offset()
    {
        var exporter = CreateExporter();
        await exporter.WriteStreamChunkAsync("key", new byte[] { 1, 2 }, isFirst: true, isLast: false);

        // Fault before any byte of the chunk reaches the server: remote size still equals committed offset.
        _world.FailNextWriteAfterBytes = 0;
        await Assert.ThrowsAsync<ExportException>(() =>
            exporter.WriteStreamChunkAsync("key", new byte[] { 3, 4 }, isFirst: false, isLast: false));

        await exporter.WriteStreamChunkAsync("key", new byte[] { 3, 4 }, isFirst: false, isLast: false);
        await exporter.WriteStreamChunkAsync("key", new byte[] { 5 }, isFirst: false, isLast: true);

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, _world.Files["/exports/key"]);
        Assert.Equal(2, _world.ConnectCount);
        Assert.Equal(1, _world.AppendCount);
    }

    [Fact]
    public async Task Resume_is_refused_when_the_server_kept_partial_bytes()
    {
        var exporter = CreateExporter();
        await exporter.WriteStreamChunkAsync("key", new byte[] { 1, 2 }, isFirst: true, isLast: false);

        // One byte of the failed chunk reached the server: remote size no longer matches the committed offset.
        _world.FailNextWriteAfterBytes = 1;
        await Assert.ThrowsAsync<ExportException>(() =>
            exporter.WriteStreamChunkAsync("key", new byte[] { 3, 4 }, isFirst: false, isLast: false));

        await Assert.ThrowsAsync<NonRetryableExportException>(() =>
            exporter.WriteStreamChunkAsync("key", new byte[] { 3, 4 }, isFirst: false, isLast: false));
    }

    [Fact]
    public async Task Retried_first_chunk_truncates_and_starts_over()
    {
        var exporter = CreateExporter();

        _world.FailNextWriteAfterBytes = 1;
        await Assert.ThrowsAsync<ExportException>(() =>
            exporter.WriteStreamChunkAsync("key", new byte[] { 1, 2 }, isFirst: true, isLast: false));

        await exporter.WriteStreamChunkAsync("key", new byte[] { 1, 2 }, isFirst: true, isLast: true);

        Assert.Equal(new byte[] { 1, 2 }, _world.Files["/exports/key"]);
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
    }
}
