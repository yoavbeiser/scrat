namespace Scrat.Exporters.Smb;

/// <summary>SMB destination settings, bound from the "Smb" section.</summary>
public sealed class SmbOptions
{
    public const string SectionName = "Smb";

    /// <summary>UNC path of the target share, e.g. <c>\\server\share</c> or <c>\\server\share\subdir</c>.</summary>
    public string SharePath { get; init; } = string.Empty;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string Domain { get; init; } = string.Empty;
}
