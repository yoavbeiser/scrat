namespace Scrat.Core.Configuration;

/// <summary>Retry/timeout tuning shared by all per-action resilience pipelines, bound from the "Resilience" section.</summary>
public sealed class ResilienceOptions
{
    public const string SectionName = "Resilience";

    public int MaxRetryAttempts { get; init; } = 3;

    public int BaseDelayMs { get; init; } = 200;

    public int MaxDelayMs { get; init; } = 5_000;

    /// <summary>Timeout applied to each individual attempt, not to the whole retried operation.</summary>
    public int AttemptTimeoutSeconds { get; init; } = 100;

    public bool UseJitter { get; init; } = true;
}
