using System.Net;
using Amazon.S3;
using Scrat.Core.Exceptions;

namespace Scrat.Core.Resilience;

/// <summary>Decides which exceptions are worth retrying.</summary>
public static class TransientErrorDetector
{
    public static bool IsTransient(Exception? exception) => exception switch
    {
        null => false,
        OperationCanceledException => false,
        NonRetryableExportException => false,
        ArgumentException or NotSupportedException or InvalidOperationException or InvalidDataException => false,
        AmazonS3Exception s3 => (int)s3.StatusCode >= 500
                                || s3.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests,
        _ => true,
    };
}
