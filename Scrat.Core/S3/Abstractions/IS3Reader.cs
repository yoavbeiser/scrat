using System.Runtime.CompilerServices;

namespace Scrat.Core.S3.Abstractions;

/// <summary>
/// Low-level S3 wire operations for one endpoint. Every method is a single atomic network
/// action, which lets a resilience decorator retry each one independently.
/// </summary>
public interface IS3Reader
{
    /// <summary>True when <paramref name="key"/> exists in <paramref name="bucketName"/> on this cluster (a HEAD on the object).</summary>
    Task<bool> ObjectExistsAsync(string bucketName, string key, CancellationToken cancellationToken = default);

    Task<byte[]> ReadAllAsync(string bucketName, string key, CancellationToken cancellationToken = default);

    Task<long> GetObjectSizeAsync(string bucketName, string key, CancellationToken cancellationToken = default);

    Task<byte[]> ReadRangeAsync(string bucketName, string key, long offset, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the object as ranged reads of <paramref name="chunkSize"/> bytes. Composed from
    /// <see cref="GetObjectSizeAsync"/> and <see cref="ReadRangeAsync"/>, so each chunk fetch is
    /// an independently retried action when the reader is decorated with resilience.
    /// </summary>
    async IAsyncEnumerable<ReadOnlyMemory<byte>> ReadChunksAsync(
        string bucketName,
        string key,
        int chunkSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(chunkSize, 0);

        var totalSize = await GetObjectSizeAsync(bucketName, key, cancellationToken).ConfigureAwait(false);
        for (long offset = 0; offset < totalSize; offset += chunkSize)
        {
            var count = (int)Math.Min(chunkSize, totalSize - offset);
            yield return await ReadRangeAsync(bucketName, key, offset, count, cancellationToken).ConfigureAwait(false);
        }
    }
}
