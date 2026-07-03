using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Scrat.Core.Exceptions;
using Scrat.Core.Models;
using Scrat.Exporters.Ftp;
using Scrat.Exporters.Ftp.Abstractions;

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

internal sealed class FakeFtpWorld
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public int ConnectCount { get; set; }

    public int AppendCount { get; set; }

    public int OpenConnections { get; set; }

    /// <summary>When set, the next stream write delivers this many bytes to the server, then throws.</summary>
    public int? FailNextWriteAfterBytes { get; set; }
}

internal sealed class FakeFtpConnectionFactory(FakeFtpWorld world) : IFtpConnectionFactory
{
    public Task<IFtpConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        world.ConnectCount++;
        world.OpenConnections++;
        return Task.FromResult<IFtpConnection>(new FakeFtpConnection(world));
    }
}

internal sealed class FakeFtpConnection(FakeFtpWorld world) : IFtpConnection
{
    public Task<Stream> OpenWriteAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        world.Files[remotePath] = [];
        return Task.FromResult<Stream>(new FakeFtpStream(world, remotePath));
    }

    public Task<Stream> OpenAppendAsync(string remotePath, CancellationToken cancellationToken = default)
    {
        world.AppendCount++;
        if (!world.Files.ContainsKey(remotePath))
        {
            world.Files[remotePath] = [];
        }

        return Task.FromResult<Stream>(new FakeFtpStream(world, remotePath));
    }

    public Task<long> GetFileSizeAsync(string remotePath, CancellationToken cancellationToken = default) =>
        Task.FromResult(world.Files.TryGetValue(remotePath, out var file) ? file.LongLength : -1);

    public ValueTask DisposeAsync()
    {
        world.OpenConnections--;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Append-only stream that commits bytes to the fake server as they are written.</summary>
internal sealed class FakeFtpStream(FakeFtpWorld world, string remotePath) : Stream
{
    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => world.Files[remotePath].Length;

    public override long Position
    {
        get => Length;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (world.FailNextWriteAfterBytes is { } deliverable)
        {
            world.FailNextWriteAfterBytes = null;
            Append(buffer.AsSpan(offset, Math.Min(deliverable, count)));
            throw new IOException("connection reset");
        }

        Append(buffer.AsSpan(offset, count));
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    private void Append(ReadOnlySpan<byte> data)
    {
        var file = world.Files[remotePath];
        var start = file.Length;
        Array.Resize(ref file, start + data.Length);
        data.CopyTo(file.AsSpan(start));
        world.Files[remotePath] = file;
    }
}
