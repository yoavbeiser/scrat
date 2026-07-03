using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrat.Core.Abstractions;
using Scrat.Core.Configuration;
using Scrat.Core.Models;

namespace Scrat.Core.Transfer;

/// <summary>Medium tier: read in chunks into memory, decode, write in sequential slices.</summary>
public sealed class MediumTransferStrategy(IOptions<TransferOptions> options, ILogger<MediumTransferStrategy> logger) : ITransferStrategy
{
    public SizeCategory Handles => SizeCategory.Medium;

    public async Task ExecuteAsync(S3EndpointMatch match, string key, IExporter exporter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(exporter);

        var transferOptions = options.Value;

        using var buffer = new MemoryStream();
        await foreach (var chunk in match.Endpoint.Reader
                           .ReadChunksAsync(match.Bucket, key, transferOptions.MediumReadChunkSizeBytes, cancellationToken)
                           .ConfigureAwait(false))
        {
            buffer.Write(chunk.Span);
        }

        var raw = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);
        var data = match.Endpoint.RequireDeserializer().Deserialize(raw);

        logger.LogDebug("Writing {Bytes} bytes for key {Key} in slices of {ChunkSize}", data.Content.Length, key, transferOptions.MediumWriteChunkSizeBytes);
        await exporter.WriteChunkedAsync(data, key, transferOptions.MediumWriteChunkSizeBytes, cancellationToken).ConfigureAwait(false);
    }
}
