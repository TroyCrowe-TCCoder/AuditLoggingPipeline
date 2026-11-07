namespace AuditLoggingPipeline;

/// <summary>Describes one record change to track through <see cref="IStateTrailRecorder"/>.</summary>
public sealed class StateTrailRecord
{
    /// <summary>What was done, for example <see cref="AuditActions.Update"/>.</summary>
    public required string Action { get; init; }

    /// <summary>The kind of entity that changed, for example <c>Role</c>.</summary>
    public required string EntityName { get; init; }

    /// <summary>The entity's identifier as text. For a composite key, use <see cref="EntityKeys"/> as well.</summary>
    public string? EntityId { get; init; }

    /// <summary>The entity's key parts by name, so a sink can fill typed key columns. When <see cref="EntityId"/> is not set, the values are joined with <c>|</c>.</summary>
    public IReadOnlyDictionary<string, string>? EntityKeys { get; init; }

    /// <summary>
    /// The record before the change, if available. An object is serialized to JSON. A string that is
    /// already valid JSON is used as is; any other string is stored as a JSON string.
    /// </summary>
    public object? BeforeState { get; init; }

    /// <summary>The full record after the change, serialized the same way as <see cref="BeforeState"/>.</summary>
    public object? AfterState { get; init; }

    /// <summary>Whether the action succeeded. Defaults to true.</summary>
    public bool Succeeded { get; init; } = true;

    /// <summary>An explanation, if any.</summary>
    public string? Reason { get; init; }
}
