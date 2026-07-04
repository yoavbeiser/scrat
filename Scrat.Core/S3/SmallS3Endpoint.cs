using Scrat.Core.Deserialization;
using Scrat.Core.Deserialization.Abstractions;
using Scrat.Core.Models;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.S3;

/// <summary>Small cluster: bucket is <c>small-data-{first two key chars, lowered}</c>; accepts any key.</summary>
public sealed class SmallS3Endpoint(IS3Reader reader, IDataDeserializer deserializer)
    : S3EndpointBase(reader, deserializer, BucketInfo.Small)
{
    public override SizeCategory HandledSizeCategory => SizeCategory.Small;
}
