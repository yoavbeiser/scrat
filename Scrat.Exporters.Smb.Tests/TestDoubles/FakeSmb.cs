using Scrat.Exporters.Smb.Abstractions;

namespace Scrat.Exporters.Smb.Tests.TestDoubles;

/// <summary>
/// In-memory SMB share used by <c>SmbExporterTests</c>. Not a mock: it stores real bytes via
/// offset-addressed writes, injects faults, and tracks connection/handle accounting, so tests can
/// assert on the reconstructed file content and on reconnect/resume behaviour.
/// </summary>
internal sealed class FakeSmbWorld
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public List<(string File, long Offset, int Length)> WriteCalls { get; } = [];

    public int ConnectCount { get; set; }

    public int CreateCount { get; set; }

    public int OpenExistingCount { get; set; }

    public int OpenConnections { get; set; }

    public Exception? FailNextWrite { get; set; }
}

internal sealed class FakeSmbConnectionFactory(FakeSmbWorld world) : ISmbConnectionFactory
{
    public Task<ISmbShareConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        world.ConnectCount++;
        world.OpenConnections++;
        return Task.FromResult<ISmbShareConnection>(new FakeSmbShareConnection(world));
    }
}

internal sealed class FakeSmbShareConnection(FakeSmbWorld world) : ISmbShareConnection
{
    public Task<ISmbFileHandle> CreateFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        world.CreateCount++;
        world.Files[fileName] = [];
        return Task.FromResult<ISmbFileHandle>(new FakeSmbFileHandle(world, fileName));
    }

    public Task<ISmbFileHandle> OpenExistingAsync(string fileName, CancellationToken cancellationToken = default)
    {
        world.OpenExistingCount++;
        if (!world.Files.ContainsKey(fileName))
        {
            throw new FileNotFoundException(fileName);
        }

        return Task.FromResult<ISmbFileHandle>(new FakeSmbFileHandle(world, fileName));
    }

    public ValueTask DisposeAsync()
    {
        world.OpenConnections--;
        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeSmbFileHandle(FakeSmbWorld world, string fileName) : ISmbFileHandle
{
    public Task WriteAsync(long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        // CR: this fake fails all-or-nothing (throws before writing any byte), but the real
        //   SmbLibraryFileHandle writes in a maxWriteSize loop and can fail *mid-object* with bytes
        //   already committed. That path is safe (writes are offset-addressed and rewrite the same
        //   region on retry) but it's untested — unlike the FTP suite, which does exercise a partial
        //   write via FailNextWriteAfterBytes. Consider a partial-write case here too.
        if (world.FailNextWrite is { } failure)
        {
            world.FailNextWrite = null;
            throw failure;
        }

        world.WriteCalls.Add((fileName, offset, data.Length));

        var file = world.Files[fileName];
        var requiredLength = (int)offset + data.Length;
        if (file.Length < requiredLength)
        {
            Array.Resize(ref file, requiredLength);
        }

        data.Span.CopyTo(file.AsSpan((int)offset));
        world.Files[fileName] = file;
        return Task.CompletedTask;
    }

    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
