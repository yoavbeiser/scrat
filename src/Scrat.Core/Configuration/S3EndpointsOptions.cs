namespace Scrat.Core.Configuration;

/// <summary>The three cluster configurations, bound from the "S3Endpoints" section.</summary>
public sealed class S3EndpointsOptions
{
    public const string SectionName = "S3Endpoints";

    public S3EndpointConfig Small { get; init; } = new();

    public S3EndpointConfig Medium { get; init; } = new();

    public S3EndpointConfig Large { get; init; } = new();
}
