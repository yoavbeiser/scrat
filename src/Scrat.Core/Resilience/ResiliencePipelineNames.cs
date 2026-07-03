namespace Scrat.Core.Resilience;

/// <summary>
/// One named pipeline per atomic I/O action, so every action is retried independently and can
/// be tuned/observed on its own.
/// </summary>
public static class ResiliencePipelineNames
{
    public const string S3BucketExists = "s3.bucket-exists";
    public const string S3ReadAll = "s3.read-all";
    public const string S3GetObjectSize = "s3.get-object-size";
    public const string S3ReadRange = "s3.read-range";
    public const string ExporterWrite = "exporter.write";
    public const string ExporterWriteChunked = "exporter.write-chunked";
    public const string ExporterWriteStreamChunk = "exporter.write-stream-chunk";

    public static readonly IReadOnlyList<string> All =
    [
        S3BucketExists,
        S3ReadAll,
        S3GetObjectSize,
        S3ReadRange,
        ExporterWrite,
        ExporterWriteChunked,
        ExporterWriteStreamChunk,
    ];
}
