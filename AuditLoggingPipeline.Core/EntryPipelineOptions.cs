using System.Threading.Channels;

namespace AuditLoggingPipeline;

/// <summary>
/// Delivery settings shared by every plugin (Audit, Tracker, Telemetry). Each plugin derives its own
/// options type so it can be configured independently.
/// </summary>
public abstract class EntryPipelineOptions
{
    /// <summary>Maximum number of entries buffered between producers and the background consumer.</summary>
    public int ChannelCapacity { get; set; } = 2048;

    /// <summary>
    /// What happens when the buffer is full. The default, <see cref="BoundedChannelFullMode.DropOldest"/>,
    /// never blocks a request, and every lost entry is counted and logged. Use
    /// <see cref="BoundedChannelFullMode.Wait"/> together with the asynchronous recording methods
    /// when entries must not be lost and brief back-pressure on the caller is acceptable.
    /// </summary>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropOldest;

    /// <summary>Number of additional attempts after a sink write fails. Zero disables retry.</summary>
    public int SinkMaxRetries { get; set; } = 2;

    /// <summary>Delay before the first retry. Each later retry doubles the previous delay.</summary>
    public TimeSpan SinkRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>Upper bound for a single sink write attempt, so a hung sink cannot stall the drain loop.</summary>
    public TimeSpan SinkWriteTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>How long shutdown waits for buffered entries to be delivered before giving up.</summary>
    public TimeSpan ShutdownDrainTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
