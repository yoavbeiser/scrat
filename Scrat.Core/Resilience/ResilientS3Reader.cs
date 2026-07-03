using Polly.Registry;
using Scrat.Core.Abstractions;

namespace Scrat.Core.Resilience;

/// <summary>
/// Decorates an <see cref="IS3Reader"/> so every atomic wire call runs inside its own resilience
/// pipeline. <see cref="IS3Reader.ReadChunksAsync"/> is inherited: it composes the decorated
/// size/range calls, so each chunk fetch is retried independently.
/// </summary>
public sealed class ResilientS3Reader(IS3Reader inner, ResiliencePipelineProvider<string> pipelineProvider) : IS3Reader
{
    public async Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.S3BucketExists)
            .ExecuteAsync(async ct => await inner.BucketExistsAsync(bucketName, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

    public async Task<byte[]> ReadAllAsync(string bucketName, string key, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.S3ReadAll)
            .ExecuteAsync(async ct => await inner.ReadAllAsync(bucketName, key, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

    public async Task<long> GetObjectSizeAsync(string bucketName, string key, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.S3GetObjectSize)
            .ExecuteAsync(async ct => await inner.GetObjectSizeAsync(bucketName, key, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);

    public async Task<byte[]> ReadRangeAsync(string bucketName, string key, long offset, int count, CancellationToken cancellationToken = default) =>
        await pipelineProvider.GetPipeline(ResiliencePipelineNames.S3ReadRange)
            .ExecuteAsync(async ct => await inner.ReadRangeAsync(bucketName, key, offset, count, ct).ConfigureAwait(false), cancellationToken)
            .ConfigureAwait(false);
}
