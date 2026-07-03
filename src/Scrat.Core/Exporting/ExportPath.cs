namespace Scrat.Core.Exporting;

/// <summary>Turns S3 keys into destination file names shared by all exporters.</summary>
public static class ExportPath
{
    private static readonly System.Buffers.SearchValues<char> InvalidChars =
        System.Buffers.SearchValues.Create("/\\:*?\"<>|");

    /// <summary>Flattens a key into a single safe file name (path separators become underscores).</summary>
    public static string ToFileName(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (key.AsSpan().IndexOfAny(InvalidChars) < 0)
        {
            return key;
        }

        return string.Create(key.Length, key, static (destination, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                destination[i] = InvalidChars.Contains(source[i]) ? '_' : source[i];
            }
        });
    }
}
