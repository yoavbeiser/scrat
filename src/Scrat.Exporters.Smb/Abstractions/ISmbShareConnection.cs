namespace Scrat.Exporters.Smb.Abstractions;

// CR: why is it interface and not use SMB directly?
/// <summary>An authenticated connection to one SMB share. Thin seam over the SMB client library.</summary>
public interface ISmbShareConnection : IAsyncDisposable
{
    /// <summary>Creates the file, truncating any existing content.</summary>
    Task<ISmbFileHandle> CreateFileAsync(string fileName, CancellationToken cancellationToken = default);

    /// <summary>Opens an existing file for writing (used to resume a stream session after a fault).</summary>
    Task<ISmbFileHandle> OpenExistingAsync(string fileName, CancellationToken cancellationToken = default);
}

// CR: why is it interface and not use SMB directly?
/// <summary>An open SMB file supporting offset-addressed writes.</summary>
public interface ISmbFileHandle : IAsyncDisposable
{
    Task WriteAsync(long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);

    Task FlushAsync(CancellationToken cancellationToken = default);
}
