using Scrat.Core.Models;

namespace Scrat.Core.Exporting.Abstractions;

/// <summary>Writes payloads or streamed byte chunks to a destination (SMB, FTP, ...).</summary>
public interface IExporter
{
    ExporterType Type { get; }

    /// <summary>
    /// Writes the full payload in one call. The exporter decides internally whether to split it
    /// (e.g. across the transport's max write size).
    /// </summary>
    Task WriteAsync(ExportData data, string key, CancellationToken cancellationToken = default);

    /// <summary>Opens a streaming session for <paramref name="key"/>, creating/truncating the destination.</summary>
    Task OpenAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes one raw chunk to the streaming session for <paramref name="key"/>. Implementations must
    /// keep retries safe: a re-invocation of a failed call writes the same chunk at the same position
    /// (or throws <see cref="Exceptions.NonRetryableExportException"/> when resuming is impossible).
    /// The exporter — not the caller — tracks position and open/close lifecycle.
    /// </summary>
    Task WriteChunkAsync(string key, ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken = default);

    /// <summary>Flushes and closes the streaming session for <paramref name="key"/>.</summary>
    Task CloseAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Releases any stream session held for <paramref name="key"/>. No-op when none exists.</summary>
    Task AbortStreamAsync(string key, CancellationToken cancellationToken = default);
}
