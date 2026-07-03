using Microsoft.Extensions.Logging;
using Scrat.Core.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.Transfer;

/// <summary>Small tier: buffer the whole object, decode it, write it atomically.</summary>
public sealed class SmallTransferStrategy(ILogger<SmallTransferStrategy> logger) : ITransferStrategy
{
    public SizeCategory Handles => SizeCategory.Small;

    public async Task ExecuteAsync(S3EndpointMatch match, string key, IExporter exporter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(exporter);

        var raw = await match.Endpoint.Reader.ReadAllAsync(match.Bucket, key, cancellationToken).ConfigureAwait(false);
        var data = match.Endpoint.RequireDeserializer().Deserialize(raw);

        logger.LogDebug("Writing {Bytes} bytes for key {Key} in a single write", data.Content.Length, key);
        await exporter.WriteAsync(data, key, cancellationToken).ConfigureAwait(false);
    }
}
