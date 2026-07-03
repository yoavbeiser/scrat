using Scrat.Core.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.S3;

/// <summary>Large cluster: bucket is <c>large-data-{type}</c>; expects keys shaped <c>{type}-{id}</c>. Bytes pass through raw.</summary>
public sealed class LargeS3Endpoint(IS3Reader reader) : S3EndpointBase(reader, deserializer: null)
{
    public override SizeCategory HandledSizeCategory => SizeCategory.Large;

    public override string? ResolveBucketName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var separatorIndex = key.IndexOf('-');
        if (separatorIndex <= 0 || separatorIndex == key.Length - 1)
        {
            return null;
        }

        return $"large-data-{key[..separatorIndex]}";
    }
}
