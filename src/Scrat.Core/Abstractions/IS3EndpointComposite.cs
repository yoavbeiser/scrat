using Scrat.Core.Models;

namespace Scrat.Core.Abstractions;

/// <summary>Probes the configured endpoints in ascending size order to find which cluster holds a key.</summary>
// CR: this is not Composite way (Composite type is the same as its leaf = IS3Endpoint) - you can remove this interface or change the type
// CR: the true purpose of this interface is Resolver (finder)
public interface IS3EndpointComposite
{
    /// <returns>The matching endpoint and bucket, or <c>null</c> when no cluster holds the key.</returns>
    Task<S3EndpointMatch?> FindEndpointAsync(string key, CancellationToken cancellationToken = default);
}
