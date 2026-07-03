using Scrat.Core.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.S3;

/// <summary>Small cluster: bucket is <c>small-data-{first two key chars, lowered}</c>; accepts any key.</summary>
public sealed class SmallS3Endpoint(IS3Reader reader, IDataDeserializer deserializer) : S3EndpointBase(reader, deserializer)
{
    public override SizeCategory HandledSizeCategory => SizeCategory.Small;

    public override string? ResolveBucketName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var prefix = key.Length >= 2 ? key[..2] : key;
        return $"small-data-{prefix.ToLowerInvariant()}";
    }
}
