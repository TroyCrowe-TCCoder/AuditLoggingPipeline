namespace AuditLoggingPipeline;

/// <summary>
/// A single "action log" style audit record: captures who called what, with what arguments,
/// and whether the call succeeded, without capturing before/after column-level state.
/// Consumers wanting state-trail semantics should implement their own sink payload/table
/// shaped for their entity (see <see cref="IAuditSink"/>); this type only carries the
/// method-invocation-level facts the <c>AuditingDispatchProxy{TInterface}</c> (Audit plugin) observes.
/// </summary>
public sealed class AuditEntry
{
    /// <summary>UTC timestamp when the audited call completed.</summary>
    public required DateTime TimestampUtc { get; init; }

    /// <summary>The audited interface/service type name.</summary>
    public required string ServiceName { get; init; }

    /// <summary>The audited method's name.</summary>
    public required string MethodName { get; init; }

    /// <summary>The authenticated caller's user identifier, if available.</summary>
    public string? UserId { get; init; }

    /// <summary>The current request's correlation/trace identifier, if available.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>A truncated, serialized representation of the method's arguments.</summary>
    public string? Arguments { get; init; }

    /// <summary>Whether the audited method call completed successfully.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>The exception message when <see cref="Succeeded"/> is false; otherwise null.</summary>
    public string? ErrorMessage { get; init; }
}
