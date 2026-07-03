using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scrat.Core.Abstractions;
using Scrat.Core.Configuration;
using Scrat.Core.Models;

namespace Scrat.Core.Services;

/// <summary>Orchestrates a transfer request: fans keys out concurrently and aggregates outcomes.</summary>
public sealed class ScratService(
    IS3EndpointComposite endpointComposite,
    ITransferStrategySelector strategySelector,
    IExporterResolver exporterResolver,
    IOptions<TransferOptions> transferOptions,
    ILogger<ScratService> logger) : IScratService
{
    public async Task<TransferResult> ExecuteAsync(TransferRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfZero(request.Keys.Count, nameof(request));

        var exporter = exporterResolver.Resolve(request.Exporter);
        var results = new KeyTransferResult[request.Keys.Count];

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = transferOptions.Value.MaxConcurrency,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, request.Keys.Count), parallelOptions, async (index, ct) =>
        {
            results[index] = await TransferKeyAsync(request.Keys[index], exporter, ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        return new TransferResult(results);
    }

    private async Task<KeyTransferResult> TransferKeyAsync(string key, IExporter exporter, CancellationToken cancellationToken)
    {
        try
        {
            var match = await endpointComposite.FindEndpointAsync(key, cancellationToken).ConfigureAwait(false);
            if (match is null)
            {
                logger.LogWarning("Key {Key} was not found on any cluster", key);
                return new KeyTransferResult(key, KeyStatus.NotFound);
            }

            var strategy = strategySelector.Select(match.Endpoint.HandledSizeCategory);
            await strategy.ExecuteAsync(match, key, exporter, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Key {Key} transferred via {Category} strategy", key, match.Endpoint.HandledSizeCategory);
            return new KeyTransferResult(key, KeyStatus.Ok);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Transfer failed for key {Key}", key);
            return new KeyTransferResult(key, KeyStatus.Failed, ex.Message);
        }
    }
}
