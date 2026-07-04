using Scrat.Exporters.Ftp.Abstractions;

namespace Scrat.Exporters.Ftp.Tests.TestDoubles;

/// <summary>
/// In-memory FTP server used by <c>FtpExporterTests</c>. Not a mock: it stores real bytes via an
/// append-only stream, reports the true remote file size, and can deliver a partial write before
/// faulting, so tests can assert on reconstructed content and on resume/append behaviour.
/// </summary>
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
