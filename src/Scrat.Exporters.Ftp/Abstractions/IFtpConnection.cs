namespace Scrat.Exporters.Ftp.Abstractions;

/// <summary>An authenticated FTP connection. Thin seam over the FTP client library.</summary>
public interface IFtpConnection : IAsyncDisposable
{
    /// <summary>Opens a write stream, creating or truncating the remote file.</summary>
    Task<Stream> OpenWriteAsync(string remotePath, CancellationToken cancellationToken = default);

    /// <summary>Opens an append stream positioned at the end of the remote file.</summary>
    Task<Stream> OpenAppendAsync(string remotePath, CancellationToken cancellationToken = default);

    /// <summary>Size of the remote file in bytes, or -1 when it does not exist.</summary>
    Task<long> GetFileSizeAsync(string remotePath, CancellationToken cancellationToken = default);
}

/// <summary>Opens authenticated connections to the configured FTP server.</summary>
public interface IFtpConnectionFactory
{
    Task<IFtpConnection> ConnectAsync(CancellationToken cancellationToken = default);
}
