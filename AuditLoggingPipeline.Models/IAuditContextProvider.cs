namespace AuditLoggingPipeline;

/// <summary>
/// Supplies the <see cref="AuditContext"/> (actor, display name, IP address, correlation id)
/// stamped onto every entry. The default implementation reads the current HTTP request. A
/// consuming application registers its own implementation when the actor or display name must
/// come from somewhere else (for example an application's active-user cache); the library
/// never assumes where those values originate.
/// </summary>
public interface IAuditContextProvider
{
    /// <summary>
    /// Resolves the context for the current operation. Called synchronously on the producing
    /// thread while the request is still active, so implementations must be fast and must not
    /// perform blocking I/O.
    /// </summary>
    AuditContext GetContext();
}
