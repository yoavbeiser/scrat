using Polly.Registry;
using Scrat.Core.Abstractions;
using Scrat.Core.Configuration;

namespace Scrat.Core.Resilience;

/// <summary>Wraps every reader produced by the inner factory in a <see cref="ResilientS3Reader"/>.</summary>
public sealed class ResilientS3ReaderFactory(IS3ReaderFactory inner, ResiliencePipelineProvider<string> pipelineProvider) : IS3ReaderFactory
{
    public IS3Reader Create(S3EndpointConfig config) => new ResilientS3Reader(inner.Create(config), pipelineProvider);
}
