namespace Scrat.Exporters.Smb.SmbLibrary;

/// <summary>Parses a UNC share path into host, share and optional base directory.</summary>
/// // CR: why implement this? cant this just be private method / static extension?
internal static class UncPath
{
    public static (string Host, string Share, string BaseDirectory) Parse(string sharePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharePath);

        var parts = sharePath.Replace('/', '\\').Trim('\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new ArgumentException($"SharePath '{sharePath}' is not a valid UNC path; expected \\\\server\\share[\\subdir].");
        }

        return (parts[0], parts[1], string.Join('\\', parts[2..]));
    }
}
