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

        // CR: only lowercasing is applied — a key whose first 2 chars contain '_', '.', or other
        //     characters illegal in an S3 bucket name produces an invalid bucket and a hard failure.
        //     Also this accepts essentially every key, which drives the misrouting noted in S3EndpointComposite.
        var prefix = key.Length >= 2 ? key[..2] : key;
        return $"small-data-{prefix.ToLowerInvariant()}";
    }
}
