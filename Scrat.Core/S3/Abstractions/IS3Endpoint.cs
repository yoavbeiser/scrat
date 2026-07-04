using Scrat.Core.Deserialization.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.S3.Abstractions;

/// <summary>One S3 cluster: its size tier, wire format decoder, bucket naming rule and reader.</summary>
public interface IS3Endpoint
{
    SizeCategory HandledSizeCategory { get; }

    /// <summary>Decoder for this cluster's wire format. <c>null</c> when bytes pass through raw (Large tier).</summary>
    IDataDeserializer? Deserializer { get; }

    IS3Reader Reader { get; }

    /// <summary>This cluster's bucket-naming rule; call <see cref="BucketInfo.Resolve"/> to map a key to its bucket.</summary>
    BucketInfo BucketInfo { get; }
}
