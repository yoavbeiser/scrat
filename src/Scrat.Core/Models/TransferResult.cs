namespace Scrat.Core.Models;

/// <summary>Aggregated result of a <see cref="TransferRequest"/>.</summary>
public sealed record TransferResult(IReadOnlyList<KeyTransferResult> Keys)
{
    public int OkCount => Count(KeyStatus.Ok);

    public int NotFoundCount => Count(KeyStatus.NotFound);

    public int FailedCount => Count(KeyStatus.Failed);

    public bool AllSucceeded => Keys.All(k => k.Status == KeyStatus.Ok);

    private int Count(KeyStatus status) => Keys.Count(k => k.Status == status);
}
