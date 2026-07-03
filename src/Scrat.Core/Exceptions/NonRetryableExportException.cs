namespace Scrat.Core.Exceptions;

/// <summary>
/// A transfer failure that cannot be fixed by retrying (e.g. a stream session whose remote state
/// no longer matches the committed offset). Resilience pipelines never retry this exception.
/// </summary>
public sealed class NonRetryableExportException : ExportException
{
    public NonRetryableExportException(string message)
        : base(message)
    {
    }

    public NonRetryableExportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
