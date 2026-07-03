namespace Scrat.Core.Abstractions;

public static class S3EndpointExtensions
{
    /// <summary>Returns the endpoint's deserializer, failing loudly when the tier has none.</summary>
    public static IDataDeserializer RequireDeserializer(this IS3Endpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return endpoint.Deserializer
               ?? throw new InvalidOperationException($"The {endpoint.HandledSizeCategory} endpoint has no deserializer configured.");
    }
}
