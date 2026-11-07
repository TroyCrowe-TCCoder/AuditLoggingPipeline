namespace AuditLoggingPipeline;

/// <summary>
/// Explicit way to record a usage observation from application code. Recording never blocks and
/// never throws; telemetry must not affect the request it observes.
/// </summary>
public interface ITelemetryRecorder
{
    /// <summary>
    /// Records an observation without waiting. Returns false if it was not kept, either because it was
    /// sampled out or because the buffer was full (a full buffer is counted as dropped).
    /// </summary>
    bool Record(UsageRecord record);

    /// <summary>
    /// Records an observation, waiting for buffer space when the pipeline is configured with
    /// <c>BoundedChannelFullMode.Wait</c>.
    /// </summary>
    ValueTask<bool> RecordAsync(UsageRecord record, CancellationToken cancellationToken = default);
}
