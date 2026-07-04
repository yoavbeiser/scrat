namespace Scrat.Core.Configuration;

/// <summary>Chunk sizes and concurrency, bound from the "TransferOptions" section.</summary>
public sealed class TransferOptions
{
    public const string SectionName = "TransferOptions";

    public int MediumReadChunkSizeBytes { get; init; } = 5 * 1024 * 1024;

    public int LargeChunkSizeBytes { get; init; } = 8 * 1024 * 1024;

    public int MaxConcurrency { get; init; } = 4;
}
