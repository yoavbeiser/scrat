using System.Globalization;
using Scrat.Core.S3.Abstractions;

namespace Scrat.Core.S3;

/// <summary>
/// A cluster's bucket-naming rule: maps a key to the bucket that would hold it, or <c>null</c> when
/// the key shape does not match the cluster's convention. Each tier's algorithm lives here; endpoints
/// expose the matching instance via <see cref="IS3Endpoint.BucketInfo"/>.
/// </summary>
public sealed class BucketInfo(Func<string, string?> resolve)
{
    /// <summary>The bucket that holds <paramref name="key"/>, or <c>null</c> when the key does not fit this cluster.</summary>
    public string? Resolve(string key) => string.IsNullOrWhiteSpace(key) ? null : resolve(key);

    /// <summary>Small cluster: <c>small-data-{first two key chars, lowered}</c>; accepts any key.</summary>
    public static BucketInfo Small { get; } = new(SmallBucket);

    /// <summary>Medium cluster: <c>medium-data-{date}</c>; expects keys shaped <c>YYYY-MM-DD/{name}</c>.</summary>
    public static BucketInfo Medium { get; } = new(MediumBucket);

    /// <summary>Large cluster: <c>large-data-{type}</c>; expects keys shaped <c>{type}-{id}</c>.</summary>
    public static BucketInfo Large { get; } = new(LargeBucket);

    private static string? SmallBucket(string key)
    {
        // CR: only lowercasing is applied — a key whose first 2 chars contain '_', '.', or other
        //     characters illegal in an S3 bucket name produces an invalid bucket and a hard failure.
        var prefix = key.Length >= 2 ? key[..2] : key;
        return $"small-data-{prefix.ToLowerInvariant()}";
    }

    private static string? MediumBucket(string key)
    {
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

    private static string? LargeBucket(string key)
    {
        var separatorIndex = key.IndexOf('-');
        if (separatorIndex <= 0 || separatorIndex == key.Length - 1)
        {
            return null;
        }

        return $"large-data-{key[..separatorIndex]}";
    }
}
