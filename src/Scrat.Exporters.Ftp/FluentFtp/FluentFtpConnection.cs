using FluentFTP;
using Scrat.Exporters.Ftp.Abstractions;

namespace Scrat.Exporters.Ftp.FluentFtp;

/// <summary>An authenticated FTP connection wrapping FluentFTP's async client.</summary>
public sealed class FluentFtpConnection(AsyncFtpClient client) : IFtpConnection
{
    public Task<Stream> OpenWriteAsync(string remotePath, CancellationToken cancellationToken = default) =>
        client.OpenWrite(remotePath, FtpDataType.Binary, checkIfFileExists: false, token: cancellationToken);

    public Task<Stream> OpenAppendAsync(string remotePath, CancellationToken cancellationToken = default) =>
        client.OpenAppend(remotePath, FtpDataType.Binary, checkIfFileExists: false, token: cancellationToken);

    public Task<long> GetFileSizeAsync(string remotePath, CancellationToken cancellationToken = default) =>
        client.GetFileSize(remotePath, defaultValue: -1, token: cancellationToken);

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (client.IsConnected)
            {
                await client.Disconnect().ConfigureAwait(false);
            }
        }
        finally
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
