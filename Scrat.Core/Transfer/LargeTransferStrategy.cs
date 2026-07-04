using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrat.Core.Configuration;
using Scrat.Core.Exporting;
using Scrat.Core.Exporting.Abstractions;
using Scrat.Core.Models;
using Scrat.Core.Transfer.Abstractions;

namespace Scrat.Core.Transfer;

/// <summary>
/// Large tier: stream chunk by chunk, never buffering the whole object. Raw bytes pass through
/// without deserialization. The exporter owns the open/close lifecycle; this strategy just reads
/// a chunk and writes it, repeating until the object is exhausted.
/// </summary>
public sealed class LargeTransferStrategy(IOptions<TransferOptions> options, ILogger<LargeTransferStrategy> logger) : ITransferStrategy
{
    public SizeCategory Handles => SizeCategory.Large;

    public async Task ExecuteAsync(S3EndpointMatch match, string key, IExporter exporter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(exporter);

        try
        {
            await StreamAsync(match, key, exporter, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await exporter.AbortStreamAsync(key, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task StreamAsync(S3EndpointMatch match, string key, IExporter exporter, CancellationToken cancellationToken)
    {
        var chunkSize = options.Value.LargeChunkSizeBytes;

        await exporter.OpenAsync(key, cancellationToken).ConfigureAwait(false);

        var chunks = 0;
        await foreach (var chunk in match.Endpoint.Reader
                           .ReadChunksAsync(match.Bucket, key, chunkSize, cancellationToken)
                           .ConfigureAwait(false))
        {
            await exporter.WriteChunkAsync(key, chunk, cancellationToken).ConfigureAwait(false);
            chunks++;
        }

        await exporter.CloseAsync(key, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Streamed key {Key} to the exporter in {Chunks} chunk(s)", key, chunks);
    }
}
