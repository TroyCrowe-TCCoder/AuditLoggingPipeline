namespace AuditLoggingPipeline;

/// <summary>
/// One informational usage observation delivered to a telemetry sink. It answers what happened, how
/// long it took and how often, for analysing how the product is used. It deliberately says nothing
/// about who did it: the only identity it can carry is an optional salted hash that is off by default.
/// This shape is independent of the audit shapes.
/// </summary>
public sealed class UsageEvent
{
    /// <summary>When the observation was made, in UTC.</summary>
    public DateTime TimestampUtc { get; init; }

    /// <summary>The application that produced the event.</summary>
    public string? Application { get; init; }

    /// <summary>The version of the application that produced the event.</summary>
    public string? Version { get; init; }

    /// <summary>The environment the application ran in, for example <c>Production</c>.</summary>
    public string? Environment { get; init; }

    /// <summary>What happened, for example <c>Report.Export</c>.</summary>
    public required string Name { get; init; }

    /// <summary>A coarse grouping used for analysis.</summary>
    public string? Category { get; init; }

    /// <summary>What the event applied to.</summary>
    public string? Target { get; init; }

    /// <summary>How long the operation took, if it was timed.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>The result of the operation. See <see cref="UsageOutcomes"/>.</summary>
    public string Outcome { get; init; } = UsageOutcomes.Success;

    /// <summary>Extra low-cardinality dimensions.</summary>
    public IReadOnlyDictionary<string, string>? Properties { get; init; }

    /// <summary>
    /// A salted hash that lets distinct-user counts be computed without identifying anyone. Null unless
    /// the application explicitly turns it on.
    /// </summary>
    public string? UserKey { get; init; }

    /// <summary>
    /// How many calls this event stands for. 1 normally; when sampling keeps one in N events, each kept
    /// event carries N so counts can still be reconstructed.
    /// </summary>
    public int SampleWeight { get; init; } = 1;
}
