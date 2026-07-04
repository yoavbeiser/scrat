using Scrat.Core.Models;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.S3;

/// <summary>Large cluster: bucket is <c>large-data-{type}</c>; expects keys shaped <c>{type}-{id}</c>. Bytes pass through raw.</summary>
public sealed class LargeS3Endpoint(IS3Reader reader)
    : S3EndpointBase(reader, deserializer: null, BucketInfo.Large)
{
    public override SizeCategory HandledSizeCategory => SizeCategory.Large;
}
