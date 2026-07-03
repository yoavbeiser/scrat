using FluentFTP;
using Microsoft.Extensions.Options;
using Scrat.Exporters.Ftp.Abstractions;

namespace Scrat.Exporters.Ftp.FluentFtp;

/// <summary>Opens FTP connections using FluentFTP.</summary>
public sealed class FluentFtpConnectionFactory(IOptions<FtpOptions> options) : IFtpConnectionFactory
{
    public async Task<IFtpConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var ftpOptions = options.Value;
        var client = new AsyncFtpClient(ftpOptions.Host, ftpOptions.Username, ftpOptions.Password, ftpOptions.Port);

        try
        {
            await client.Connect(cancellationToken).ConfigureAwait(false);
            return new FluentFtpConnection(client);
        }
        catch
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
