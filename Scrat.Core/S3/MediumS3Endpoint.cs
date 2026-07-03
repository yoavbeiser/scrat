using System.Globalization;
using Scrat.Core.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.S3;

/// <summary>Medium cluster: bucket is <c>medium-data-{date}</c>; expects keys shaped <c>YYYY-MM-DD/{name}</c>.</summary>
public sealed class MediumS3Endpoint(IS3Reader reader, IDataDeserializer deserializer) : S3EndpointBase(reader, deserializer)
{
    public override SizeCategory HandledSizeCategory => SizeCategory.Medium;

    public override string? ResolveBucketName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var separatorIndex = key.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex == key.Length - 1)
        {
            return null;
        }

        var datePart = key[..separatorIndex];
        if (!DateOnly.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            return null;
        }

        return $"medium-data-{datePart}";
    }
}
