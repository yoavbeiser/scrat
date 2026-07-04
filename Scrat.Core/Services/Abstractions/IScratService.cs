using Scrat.Core.Models;

namespace Scrat.Core.Services.Abstractions;

/// <summary>Public entry point: fans keys out concurrently and aggregates per-key outcomes.</summary>
public interface IScratService
{
    Task<TransferResult> ExecuteAsync(TransferRequest request, CancellationToken cancellationToken = default);
}
