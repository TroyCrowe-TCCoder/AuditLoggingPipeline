namespace AuditLoggingPipeline;

/// <summary>
/// A record-level state-trail entry: the action taken on one entity together with a JSON snapshot
/// of that record. The shape follows the per-table audit stores (action type, full record state as
/// JSON, actor display name, IP address). Produced either explicitly through
/// <see cref="IStateTrailRecorder"/> or by the Tracker plugin's dispatch proxy.
/// </summary>
public sealed class StateTrailEntry
{
    /// <summary>UTC timestamp when the tracked action completed.</summary>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>What was done to the record, for example <see cref="AuditActions.Update"/>.</summary>
    public string? Action { get; init; }

    /// <summary>The kind of entity that was changed, for example <c>Role</c>.</summary>
    public required string EntityName { get; init; }

    /// <summary>The entity's identifier as text. For a composite key this is the key parts joined with <c>|</c>.</summary>
    public string? EntityId { get; init; }

    /// <summary>The entity's key parts by name (for example <c>UserId</c> and <c>RoleId</c>), so a sink can fill typed key columns.</summary>
    public IReadOnlyDictionary<string, string>? EntityKeys { get; init; }

    /// <summary>The acting principal's identifier, if known.</summary>
    public string? UserId { get; init; }

    /// <summary>The acting principal's display name, if known.</summary>
    public string? ActorDisplayName { get; init; }

    /// <summary>The caller's IP address, if known.</summary>
    public string? IpAddress { get; init; }

    /// <summary>The request's correlation/trace identifier, if known.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>JSON snapshot of the record before the change, when the caller supplied one.</summary>
    public string? BeforeState { get; init; }

    /// <summary>JSON snapshot of the full record after the change. This is the value a sink stores as the record state.</summary>
    public string? AfterState { get; init; }

    /// <summary>Whether the tracked action completed successfully.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>A caller-supplied explanation, if any.</summary>
    public string? Reason { get; init; }

    /// <summary>The tracked interface/service type name, when the entry came from the dispatch proxy.</summary>
    public string? ServiceName { get; init; }

    /// <summary>The tracked method's name, when the entry came from the dispatch proxy.</summary>
    public string? MethodName { get; init; }

    /// <summary>The exception message when <see cref="Succeeded"/> is false because the call threw; otherwise null.</summary>
    public string? ErrorMessage { get; init; }
}
