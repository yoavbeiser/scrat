namespace Scrat.Core.Configuration;

/// <summary>Connection settings for one S3 cluster.</summary>
public sealed class S3EndpointConfig
{
    public string ServiceUrl { get; init; } = string.Empty;

    public string AccessKey { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string Region { get; init; } = "us-east-1";
}
