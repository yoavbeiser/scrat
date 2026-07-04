namespace Scrat.Core.Resilience;

/// <summary>
/// One named pipeline per atomic I/O action, so every action is retried independently and can
/// be tuned/observed on its own.
/// </summary>
public static class ResiliencePipelineNames
{
    public const string S3ObjectExists = "s3.object-exists";
    public const string S3ReadAll = "s3.read-all";
    public const string S3GetObjectSize = "s3.get-object-size";
    public const string S3ReadRange = "s3.read-range";
    public const string ExporterWrite = "exporter.write";
    public const string ExporterOpen = "exporter.open";
    public const string ExporterWriteChunk = "exporter.write-chunk";
    public const string ExporterClose = "exporter.close";

    public static readonly IReadOnlyList<string> All =
    [
        S3ObjectExists,
        S3ReadAll,
        S3GetObjectSize,
        S3ReadRange,
        ExporterWrite,
        ExporterOpen,
        ExporterWriteChunk,
        ExporterClose,
    ];
}
