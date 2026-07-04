using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.S3;

/// <summary>AWS SDK implementation of the atomic S3 wire operations.</summary>
public sealed class AwsS3Reader(IAmazonS3 client) : IS3Reader
{
    public async Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            await client.HeadBucketAsync(new HeadBucketRequest { BucketName = bucketName }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MovedPermanently or HttpStatusCode.Forbidden)
        {
            // 301/403 mean the bucket lives elsewhere or is not ours — either way this cluster does not hold the key.
            return false;
        }
    }

    public async Task<byte[]> ReadAllAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        using var response = await client.GetObjectAsync(bucketName, key, cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    public async Task<long> GetObjectSizeAsync(string bucketName, string key, CancellationToken cancellationToken = default)
    {
        var response = await client.GetObjectMetadataAsync(bucketName, key, cancellationToken).ConfigureAwait(false);
        return response.ContentLength;
    }

    public async Task<byte[]> ReadRangeAsync(string bucketName, string key, long offset, int count, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0);

        var request = new GetObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            ByteRange = new ByteRange(offset, offset + count - 1),
        };

        using var response = await client.GetObjectAsync(request, cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream(count);
        await response.ResponseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
