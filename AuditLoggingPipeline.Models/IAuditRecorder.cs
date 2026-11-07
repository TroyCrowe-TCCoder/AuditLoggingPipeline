namespace AuditLoggingPipeline;

/// <summary>
/// Explicit way to record a security audit event from application code. The actor, display name, IP
/// address and correlation id are captured synchronously from the registered
/// <see cref="IAuditContextProvider"/> at the moment of the call, so they always belong to the
/// request that made it.
/// </summary>
public interface IAuditRecorder
{
    /// <summary>Records an event without waiting. Returns false if the entry was not accepted (counted as dropped).</summary>
    bool Record(AuditRecord record);

    /// <summary>
    /// Records an event, waiting for buffer space when the pipeline is configured with
    /// <c>BoundedChannelFullMode.Wait</c>. Use for events that must not be lost.
    /// </summary>
    ValueTask<bool> RecordAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
