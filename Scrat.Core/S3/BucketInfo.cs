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
        var prefix = (key.Length >= 2 ? key[..2] : key).ToLowerInvariant();

        // The prefix is spliced into an S3 bucket name, which only allows [a-z0-9-]. If the key's
        // first chars fall outside that set, this cluster can't hold it — return null so the tier is
        // skipped cleanly rather than probing a malformed bucket name.
        foreach (var c in prefix)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return null;
            }
        }

        return $"small-data-{prefix}";
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
