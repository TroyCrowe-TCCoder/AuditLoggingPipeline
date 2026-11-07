namespace AuditLoggingPipeline;

/// <summary>
/// Optional. Supplies a stable identifier for the current user so the telemetry recorder can hash it
/// into <see cref="UsageEvent.UserKey"/>. It is only consulted when the application turns the user
/// key on. The value is hashed before it leaves the recorder and is never stored as returned.
/// </summary>
public interface IUsageUserKeyProvider
{
    /// <summary>Returns a stable identifier for the current user, or null when there is none.</summary>
    string? GetUserIdentifier();
}
