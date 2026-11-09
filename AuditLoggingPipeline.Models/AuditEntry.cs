namespace AuditLoggingPipeline;

/// <summary>
/// A security-level audit record: who did what, when, with what outcome, to which entity. The
/// shape follows the audit store's "Who / What / When / Outcome / CorrelationId / EntityType /
/// EntityId / Reason / IpAddress" columns so a sink can write it without reshaping. Produced either
/// explicitly through <see cref="IAuditRecorder"/> or by the Audit plugin's dispatch proxy.
/// </summary>
public sealed class AuditEntry
{
    /// <summary>The value of <see cref="Outcome"/> when <see cref="Succeeded"/> is true.</summary>
    public const string SuccessOutcome = "Success";

    /// <summary>The value of <see cref="Outcome"/> when <see cref="Succeeded"/> is false.</summary>
    public const string FailureOutcome = "Failure";

    /// <summary>UTC timestamp when the audited action completed.</summary>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>What happened, for example <c>Role.Upsert</c>. See <see cref="AuditActions"/> for common verbs.</summary>
    public string? Action { get; init; }

    /// <summary>Whether the audited action completed successfully.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Either <see cref="SuccessOutcome"/> or <see cref="FailureOutcome"/>.</summary>
    public string Outcome => Succeeded ? SuccessOutcome : FailureOutcome;

    /// <summary>The acting principal's identifier, if known.</summary>
    public string? UserId { get; init; }

    /// <summary>The acting principal's display name, if known.</summary>
    public string? ActorDisplayName { get; init; }

    /// <summary>The caller's IP address, if known.</summary>
    public string? IpAddress { get; init; }

    /// <summary>The request's correlation/trace identifier, if known.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>The kind of entity acted upon, for example <c>Role</c>.</summary>
    public string? EntityType { get; init; }

    /// <summary>The identifier of the entity acted upon, as text so any key type fits.</summary>
    public string? EntityId { get; init; }

    /// <summary>The entity's key parts by name (for example <c>UserId</c> and <c>RoleId</c>), when the caller supplied them.</summary>
    public IReadOnlyDictionary<string, string>? EntityKeys { get; init; }

    /// <summary>A caller-supplied explanation, for example why an authorization check failed.</summary>
    public string? Reason { get; init; }

    /// <summary>The audited interface/service type name, when the entry came from the dispatch proxy.</summary>
    public string? ServiceName { get; init; }

    /// <summary>The audited method's name, when the entry came from the dispatch proxy.</summary>
    public string? MethodName { get; init; }

    /// <summary>
    /// Serialized call arguments. Only populated when the Audit plugin is configured with
    /// <c>IncludeArguments</c>; off by default because arguments can carry sensitive data.
    /// </summary>
    public string? Arguments { get; init; }

    /// <summary>The exception message when <see cref="Succeeded"/> is false because the call threw; otherwise null.</summary>
    public string? ErrorMessage { get; init; }
}
