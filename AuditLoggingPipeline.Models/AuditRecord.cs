namespace AuditLoggingPipeline;

/// <summary>Describes one audit event to record through <see cref="IAuditRecorder"/>.</summary>
public sealed class AuditRecord
{
    /// <summary>What happened, for example <c>Role.Upsert</c> or <c>Login</c>.</summary>
    public required string Action { get; init; }

    /// <summary>The kind of entity acted upon, for example <c>Role</c>.</summary>
    public required string EntityType { get; init; }

    /// <summary>The identifier of the entity acted upon, as text, if there is one.</summary>
    public string? EntityId { get; init; }

    /// <summary>Whether the action succeeded. Defaults to true.</summary>
    public bool Succeeded { get; init; } = true;

    /// <summary>An explanation, for example why an authorization check failed.</summary>
    public string? Reason { get; init; }
}
