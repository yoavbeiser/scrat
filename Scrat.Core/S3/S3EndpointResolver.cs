using Microsoft.Extensions.Logging;
using Scrat.Core.Models;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.S3;

/// <summary>Probes endpoints in ascending size order; the first cluster whose bucket exists wins.</summary>
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

            // CR: checking if bucket exists is not the same as key exists
            // CR: BucketInfo.Small resolves a bucket for ANY non-empty key, so Small is always probed
            //     first (ordered ascending). If its bucket exists, it wins even for keys meant for
            //     Medium/Large -> misrouting. Combined with the note above, a key that is genuinely
            //     absent from the winning bucket surfaces as Failed (GetObject 404), never NotFound.
            //     Consider probing the object (HEAD key), not just the bucket.
            if (await endpoint.Reader.BucketExistsAsync(bucket, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogDebug("Key {Key} resolved to {Category} cluster (bucket {Bucket})", key, endpoint.HandledSizeCategory, bucket);
                return new S3EndpointMatch(endpoint, bucket);
            }
        }

        return null;
    }
}
