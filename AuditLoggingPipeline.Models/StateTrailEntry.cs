namespace AuditLoggingPipeline;

/// <summary>
/// A single "state trail" style record: captures the before/after values of an entity's
/// tracked properties for a single mutating call, independent of the action-log semantics
/// carried by <see cref="AuditEntry"/>. Produced by the Tracker plugin's DispatchProxy-based
/// interceptor and persisted by consumer-registered <see cref="IEntrySink{TEntry}"/> implementations
/// (i.e. <c>IEntrySink&lt;StateTrailEntry&gt;</c>).
/// </summary>
public sealed class StateTrailEntry
{
    public required DateTime TimestampUtc { get; init; }

    public required string ServiceName { get; init; }

    public required string MethodName { get; init; }

    public required string EntityName { get; init; }

    public string? EntityId { get; init; }

    public string? UserId { get; init; }

    public string? CorrelationId { get; init; }

    /// <summary>Serialized snapshot of tracked property values before the call, if available.</summary>
    public string? BeforeState { get; init; }

    /// <summary>Serialized snapshot of tracked property values after the call.</summary>
    public string? AfterState { get; init; }

    public required bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }
}
