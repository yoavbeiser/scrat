namespace Scrat.Core.Exceptions;

/// <summary>A transfer-related failure. Considered transient and retried by the resilience pipelines.</summary>
public class ExportException : Exception
{
    public ExportException(string message)
        : base(message)
    {
    }

    public ExportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
