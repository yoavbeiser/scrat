namespace Scrat.Exporters.Ftp;

/// <summary>FTP destination settings, bound from the "Ftp" section.</summary>
public sealed class FtpOptions
{
    public const string SectionName = "Ftp";

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; } = 21;

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string BasePath { get; init; } = "/";
}
