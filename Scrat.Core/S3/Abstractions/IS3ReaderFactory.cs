using Scrat.Core.Configuration;

namespace Scrat.Core.S3.Abstractions;

/// <summary>Constructs an <see cref="IS3Reader"/> for one endpoint. Injection point for swapping the AWS SDK.</summary>
public interface IS3ReaderFactory
{
    IS3Reader Create(S3EndpointConfig config);
}
