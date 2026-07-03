using Scrat.Core.Exceptions;
using Scrat.Exporters.Smb.Abstractions;
using SMBLibrary;
using SMBLibrary.Client;

namespace Scrat.Exporters.Smb.SmbLibrary;

/// <summary>An authenticated SMB2 tree connection wrapping SMBLibrary's file store.</summary>
public sealed class SmbLibraryShareConnection(SMB2Client client, ISMBFileStore fileStore, string baseDirectory) : ISmbShareConnection
{
    public Task<ISmbFileHandle> CreateFileAsync(string fileName, CancellationToken cancellationToken = default) =>
        OpenAsync(fileName, CreateDisposition.FILE_OVERWRITE_IF, cancellationToken);

    public Task<ISmbFileHandle> OpenExistingAsync(string fileName, CancellationToken cancellationToken = default) =>
        OpenAsync(fileName, CreateDisposition.FILE_OPEN, cancellationToken);

    public ValueTask DisposeAsync()
    {
        try
        {
            fileStore.Disconnect();
            client.Logoff();
        }
        finally
        {
            client.Disconnect();
        }

        return ValueTask.CompletedTask;
    }

    private Task<ISmbFileHandle> OpenAsync(string fileName, CreateDisposition disposition, CancellationToken cancellationToken)
    {
        return Task.Run<ISmbFileHandle>(() =>
        {
            var path = string.IsNullOrEmpty(baseDirectory) ? fileName : $"{baseDirectory}\\{fileName}";

            var status = fileStore.CreateFile(
                out var handle,
                out _,
                path,
                AccessMask.GENERIC_WRITE | AccessMask.SYNCHRONIZE,
                SMBLibrary.FileAttributes.Normal,
                ShareAccess.Read,
                disposition,
                CreateOptions.FILE_NON_DIRECTORY_FILE | CreateOptions.FILE_SYNCHRONOUS_IO_NONALERT,
                null);

            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new ExportException($"SMB open of '{path}' failed with status {status}.");
            }

            return new SmbLibraryFileHandle(fileStore, handle, (int)client.MaxWriteSize);
        }, cancellationToken);
    }
}
