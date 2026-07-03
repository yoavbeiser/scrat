using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrat.Core.Abstractions;
using Scrat.Core.Configuration;
using Scrat.Core.Models;

namespace Scrat.Core.Transfer;

/// <summary>
/// Large tier: stream chunk by chunk, never buffering the whole object. Raw bytes pass through
/// without deserialization; <c>isFirst</c>/<c>isLast</c> signal the exporter to open/close the file.
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
        var isFirst = true;

        // CR: the process should be: s3 read chunk -> exporter write chunk -> repeat 
        // One chunk of lookahead so the final chunk can be flagged isLast.
        ReadOnlyMemory<byte>? pending = null;
        await foreach (var chunk in match.Endpoint.Reader
                           .ReadChunksAsync(match.Bucket, key, chunkSize, cancellationToken)
                           .ConfigureAwait(false))
        {
            if (pending is { } previous)
            {
                await exporter.WriteStreamChunkAsync(key, previous, isFirst, isLast: false, cancellationToken).ConfigureAwait(false);
                isFirst = false;
            }

            pending = chunk;
        }

        var lastChunk = pending ?? ReadOnlyMemory<byte>.Empty;
        logger.LogDebug("Writing final stream chunk of {Bytes} bytes for key {Key}", lastChunk.Length, key);
        await exporter.WriteStreamChunkAsync(key, lastChunk, isFirst, isLast: true, cancellationToken).ConfigureAwait(false);
    }
}
