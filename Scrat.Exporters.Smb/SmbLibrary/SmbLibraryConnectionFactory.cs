using Microsoft.Extensions.Options;
using Scrat.Core.Exceptions;
using Scrat.Exporters.Smb.Abstractions;
using SMBLibrary;
using SMBLibrary.Client;

namespace Scrat.Exporters.Smb.SmbLibrary;

/// <summary>Opens SMB2 connections using the SMBLibrary client.</summary>
public sealed class SmbLibraryConnectionFactory(IOptions<SmbOptions> options) : ISmbConnectionFactory
{
    public Task<ISmbShareConnection> ConnectAsync(CancellationToken cancellationToken = default)
    {
        // SMBLibrary is synchronous; run the handshake off the caller's context.
        return Task.Run<ISmbShareConnection>(() =>
        {
            var smbOptions = options.Value;
            var (host, share, baseDirectory) = UncPath.Parse(smbOptions.SharePath);

            var client = new SMB2Client();
            if (!client.Connect(host, SMBTransportType.DirectTCPTransport))
            {
                throw new ExportException($"Cannot connect to SMB server '{host}'.");
            }

            try
            {
                var status = client.Login(smbOptions.Domain, smbOptions.Username, smbOptions.Password);
                ThrowOnError(status, $"login to '{host}'");

                var fileStore = client.TreeConnect(share, out status);
                ThrowOnError(status, $"tree connect to share '{share}'");

                return new SmbLibraryShareConnection(client, fileStore, baseDirectory);
            }
            catch
            {
                client.Disconnect();
                throw;
            }
        }, cancellationToken);
    }

    internal static void ThrowOnError(NTStatus status, string action)
    {
        if (status != NTStatus.STATUS_SUCCESS)
        {
            throw new ExportException($"SMB {action} failed with status {status}.");
        }
    }
}
