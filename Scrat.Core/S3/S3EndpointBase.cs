using Scrat.Core.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.S3;

/// <summary>Common wiring for the three cluster endpoints; subclasses supply the bucket naming rule.</summary>
public abstract class S3EndpointBase(IS3Reader reader, IDataDeserializer? deserializer) : IS3Endpoint
{
    public abstract SizeCategory HandledSizeCategory { get; }

    public IDataDeserializer? Deserializer { get; } = deserializer;

    public IS3Reader Reader { get; } = reader ?? throw new ArgumentNullException(nameof(reader));

    public abstract string? ResolveBucketName(string key);
}
