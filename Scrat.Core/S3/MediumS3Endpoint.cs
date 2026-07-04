using Scrat.Core.Deserialization;
using Scrat.Core.Deserialization.Abstractions;
using Scrat.Core.Models;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.S3;

/// <summary>Medium cluster: bucket is <c>medium-data-{date}</c>; expects keys shaped <c>YYYY-MM-DD/{name}</c>.</summary>
public sealed class MediumS3Endpoint(IS3Reader reader, IDataDeserializer deserializer)
    : S3EndpointBase(reader, deserializer, BucketInfo.Medium)
{
    public override SizeCategory HandledSizeCategory => SizeCategory.Medium;
}
