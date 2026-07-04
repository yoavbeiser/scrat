using Polly.Registry;
using Scrat.Core.Exporting.Abstractions;
using Scrat.Core.Models;

namespace Scrat.Core.Resilience;

/// <summary>
/// Decorates an <see cref="IExporter"/> so each write call runs inside its own resilience
/// pipeline. Aborts are cleanup and pass through unretried.
/// </summary>
public sealed class ResilientExporter(IExporter inner, ResiliencePipelineProvider<string> pipelineProvider) : IExporter
{
    public ExporterType Type => inner.Type;

    public async Task WriteAsync(ExportData data, string key, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.ExporterWrite)
            .ExecuteAsync(async ct => await inner.WriteAsync(data, key, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

    public async Task OpenAsync(string key, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.ExporterOpen)
            .ExecuteAsync(async ct => await inner.OpenAsync(key, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

    public async Task WriteChunkAsync(string key, ReadOnlyMemory<byte> chunk, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.ExporterWriteChunk)
            .ExecuteAsync(async ct => await inner.WriteChunkAsync(key, chunk, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

    public async Task CloseAsync(string key, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.ExporterClose)
            .ExecuteAsync(async ct => await inner.CloseAsync(key, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

    public Task AbortStreamAsync(string key, CancellationToken cancellationToken = default) =>
        inner.AbortStreamAsync(key, cancellationToken);
}
