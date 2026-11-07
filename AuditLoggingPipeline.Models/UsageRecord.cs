namespace AuditLoggingPipeline;

/// <summary>
/// Describes one usage observation to record through <see cref="ITelemetryRecorder"/>. The recorder
/// adds the timestamp, application, version and environment.
/// </summary>
public sealed class UsageRecord
{
    /// <summary>What happened, for example <c>Report.Export</c> or <c>Search</c>.</summary>
    public required string Name { get; init; }

    /// <summary>A coarse grouping used for analysis, for example <c>Feature</c>, <c>Method</c> or <c>Page</c>.</summary>
    public string? Category { get; init; }

    /// <summary>What the event applied to, for example a feature, endpoint or service name. Never an individual record id.</summary>
    public string? Target { get; init; }

    /// <summary>How long the operation took, if it was timed.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>The result of the operation. See <see cref="UsageOutcomes"/>. Defaults to success.</summary>
    public string Outcome { get; init; } = UsageOutcomes.Success;

    /// <summary>Extra low-cardinality dimensions for analysis. Do not put personal data or free text here.</summary>
    public IReadOnlyDictionary<string, string>? Properties { get; init; }
}
