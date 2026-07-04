using Scrat.Core.Models;

namespace Scrat.Core.S3.Abstractions;

/// <summary>Probes the configured endpoints in ascending size order to find which cluster holds a key.</summary>
public interface IS3EndpointResolver
{
    /// <returns>The matching endpoint and bucket, or <c>null</c> when no cluster holds the key.</returns>
    Task<S3EndpointMatch?> FindEndpointAsync(string key, CancellationToken cancellationToken = default);
}
