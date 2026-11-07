namespace AuditLoggingPipeline;

/// <summary>
/// The ambient facts about "who is acting, from where, in which request" that are captured at
/// the moment an audit or state-trail entry is produced. Resolved by an
/// <see cref="IAuditContextProvider"/> while still inside the request, so the values are never
/// read from a request that has already completed.
/// </summary>
public sealed class AuditContext
{
    /// <summary>An empty context, used when no ambient request information is available.</summary>
    public static AuditContext Empty { get; } = new();

    /// <summary>The acting principal's identifier (for example a subject or object id), if known.</summary>
    public string? ActorId { get; init; }

    /// <summary>The acting principal's display name, if known.</summary>
    public string? ActorDisplayName { get; init; }

    /// <summary>The caller's IP address, if known.</summary>
    public string? IpAddress { get; init; }

    /// <summary>The current request's correlation identifier, if known.</summary>
    public string? CorrelationId { get; init; }
}
