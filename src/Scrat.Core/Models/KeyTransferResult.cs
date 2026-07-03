namespace Scrat.Core.Models;

/// <summary>Per-key transfer outcome.</summary>
public sealed record KeyTransferResult(string Key, KeyStatus Status, string? Error = null);
