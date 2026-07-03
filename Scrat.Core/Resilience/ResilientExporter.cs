using Polly.Registry;
using Scrat.Core.Abstractions;
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

    public async Task WriteChunkedAsync(ExportData data, string key, int chunkSizeBytes, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.ExporterWriteChunked)
            .ExecuteAsync(async ct => await inner.WriteChunkedAsync(data, key, chunkSizeBytes, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

    public async Task WriteStreamChunkAsync(string key, ReadOnlyMemory<byte> chunk, bool isFirst, bool isLast, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.ExporterWriteStreamChunk)
            .ExecuteAsync(async ct => await inner.WriteStreamChunkAsync(key, chunk, isFirst, isLast, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

    public Task AbortStreamAsync(string key, CancellationToken cancellationToken = default) =>
        inner.AbortStreamAsync(key, cancellationToken);
}
