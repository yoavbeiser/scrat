using Microsoft.Extensions.Logging;
using Scrat.Core.Models;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.S3;

/// <summary>Probes endpoints in ascending size order; the first cluster that actually holds the key wins.</summary>
public sealed class S3EndpointResolver : IS3EndpointResolver
{
    private readonly IReadOnlyList<IS3Endpoint> _endpoints;
    private readonly ILogger<S3EndpointResolver> _logger;

    public S3EndpointResolver(IEnumerable<IS3Endpoint> endpoints, ILogger<S3EndpointResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        _endpoints = endpoints.OrderBy(e => e.HandledSizeCategory).ToArray();
        _logger = logger;
    }

    public async Task<S3EndpointMatch?> FindEndpointAsync(string key, CancellationToken cancellationToken = default)
    {
        foreach (var endpoint in _endpoints)
        {
            var bucket = endpoint.BucketInfo.Resolve(key);
            if (bucket is null)
            {
                continue;
            }

            // Probe the object itself (not just the bucket): BucketInfo.Small resolves a bucket for
            // almost any key, so a bucket-only check would misroute keys that also fit Medium/Large and
            // would report a genuinely missing key as a failed read rather than NotFound.
            if (await endpoint.Reader.ObjectExistsAsync(bucket, key, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogDebug("Key {Key} resolved to {Category} cluster (bucket {Bucket})", key, endpoint.HandledSizeCategory, bucket);
                return new S3EndpointMatch(endpoint, bucket);
            }
        }

        return null;
    }
}
