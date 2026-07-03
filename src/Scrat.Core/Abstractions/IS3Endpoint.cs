using Scrat.Core.Models;

namespace Scrat.Core.Abstractions;

/// <summary>One S3 cluster: its size tier, wire format decoder, bucket naming rule and reader.</summary>
public interface IS3Endpoint
{
    SizeCategory HandledSizeCategory { get; }

    /// <summary>Decoder for this cluster's wire format. <c>null</c> when bytes pass through raw (Large tier).</summary>
    IDataDeserializer? Deserializer { get; }

    IS3Reader Reader { get; }

    /// <summary>
    /// Derives the bucket name that would hold <paramref name="key"/> on this cluster, or
    /// <c>null</c> when the key shape does not match the cluster's naming convention.
    /// </summary>
    // CR: the logic that resolve the bucketName as in static class named BucketInfo (create it) - its algorithm is calculated using number of buckets
    // CR: you can add BucketInfo in IS3Endpoint and initialize it in implementation
    string? ResolveBucketName(string key);
}
