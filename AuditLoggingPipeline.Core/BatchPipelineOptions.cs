namespace AuditLoggingPipeline;

/// <summary>
/// Delivery settings for plugins that hand entries to their sinks in batches, such as Telemetry. It
/// adds batching to the settings every plugin shares.
/// </summary>
public abstract class BatchPipelineOptions : EntryPipelineOptions
{
    /// <summary>The largest number of entries delivered to a sink in one call. Values below 1 are treated as 1.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// How long the dispatcher waits for a partly filled batch to fill up before delivering it anyway, so
    /// a quiet period never leaves entries sitting in the buffer. Zero delivers whatever is available at once.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);
}
