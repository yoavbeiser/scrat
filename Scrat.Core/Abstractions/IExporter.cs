using Scrat.Core.Models;

namespace Scrat.Core.Abstractions;
// CR: add open connection
// CR: add close connection
/// <summary>Writes payloads or raw byte chunks to a destination (SMB, FTP, ...).</summary>
public interface IExporter
{
    ExporterType Type { get; }

    /// <summary>Writes the full payload in one call.</summary>
    Task WriteAsync(ExportData data, string key, CancellationToken cancellationToken = default);

    // CR: no need for this method (the exporter will decide whether to write full or parts)
    /// <summary>Writes the payload in sequential slices of <paramref name="chunkSizeBytes"/>.</summary>
    Task WriteChunkedAsync(ExportData data, string key, int chunkSizeBytes, CancellationToken cancellationToken = default);
    
    // CR: this method should not receive bool isFirst, bool isLast - it is the Exporter responsibility
    /// <summary>
    /// Writes one raw chunk of a streamed transfer. <paramref name="isFirst"/> opens the
    /// destination file, <paramref name="isLast"/> flushes and closes it. Implementations must
    /// keep retries safe: a re-invocation of a failed call writes the same chunk at the same
    /// position (or throws <see cref="Exceptions.NonRetryableExportException"/> when resuming is impossible).
    /// </summary>
    Task WriteStreamChunkAsync(string key, ReadOnlyMemory<byte> chunk, bool isFirst, bool isLast, CancellationToken cancellationToken = default);

    /// <summary>Releases any stream session held for <paramref name="key"/>. No-op when none exists.</summary>
    Task AbortStreamAsync(string key, CancellationToken cancellationToken = default);
}
