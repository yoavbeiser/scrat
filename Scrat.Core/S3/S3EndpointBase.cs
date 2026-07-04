using Scrat.Core.Deserialization;
using Scrat.Core.Deserialization.Abstractions;
using Scrat.Core.Models;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.S3;

/// <summary>Common wiring for the three cluster endpoints; subclasses supply the bucket naming rule.</summary>
public abstract class S3EndpointBase(IS3Reader reader, IDataDeserializer? deserializer, BucketInfo bucketInfo) : IS3Endpoint
{
    public abstract SizeCategory HandledSizeCategory { get; }

    public IDataDeserializer? Deserializer { get; } = deserializer;

    public IS3Reader Reader { get; } = reader ?? throw new ArgumentNullException(nameof(reader));

    public BucketInfo BucketInfo { get; } = bucketInfo ?? throw new ArgumentNullException(nameof(bucketInfo));
}
