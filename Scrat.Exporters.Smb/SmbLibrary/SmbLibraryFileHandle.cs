using System.Runtime.InteropServices;
using Scrat.Core.Exceptions;
using Scrat.Exporters.Smb.Abstractions;
using SMBLibrary;
using SMBLibrary.Client;

namespace Scrat.Exporters.Smb.SmbLibrary;

/// <summary>An open SMB file; splits writes to honour the negotiated max write size.</summary>
public sealed class SmbLibraryFileHandle(ISMBFileStore fileStore, object handle, int maxWriteSize) : ISmbFileHandle
{
    public Task WriteAsync(long offset, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var written = 0;
            while (written < data.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var sliceLength = Math.Min(maxWriteSize, data.Length - written);
                var status = fileStore.WriteFile(
                    out var bytesWritten,
                    handle,
                    offset + written,
                    AsArray(data.Slice(written, sliceLength)));

                if (status != NTStatus.STATUS_SUCCESS)
                {
                    throw new ExportException($"SMB write at offset {offset + written} failed with status {status}.");
                }

                if (bytesWritten <= 0)
                {
                    throw new ExportException($"SMB write at offset {offset + written} made no progress.");
                }

                written += bytesWritten;
            }
        }, cancellationToken);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var status = fileStore.FlushFileBuffers(handle);
            if (status != NTStatus.STATUS_SUCCESS)
            {
                throw new ExportException($"SMB flush failed with status {status}.");
            }
        }, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        fileStore.CloseFile(handle);
        return ValueTask.CompletedTask;
    }

    // SMBLibrary.WriteFile takes a byte[]. Hand it the backing array directly when the slice already
    // is a whole standalone array (the common case: a full chunk that fits one write), and only copy
    // when the slice is a sub-range — avoiding a fresh allocation on every write.
    private static byte[] AsArray(ReadOnlyMemory<byte> memory) =>
        MemoryMarshal.TryGetArray(memory, out var segment) && segment.Offset == 0 && segment.Count == segment.Array!.Length
            ? segment.Array
            : memory.ToArray();
}
