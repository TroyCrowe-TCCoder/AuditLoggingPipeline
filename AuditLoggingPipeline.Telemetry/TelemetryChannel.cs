using Microsoft.Extensions.Options;

namespace AuditLoggingPipeline;

/// <summary>
/// Singleton bounded channel connecting telemetry producers to the single batching consumer
/// (<see cref="TelemetryBackgroundService"/>). Lost events are counted in
/// <see cref="BoundedEntryChannel{TEntry}.DroppedCount"/>.
/// </summary>
public sealed class TelemetryChannel : BoundedEntryChannel<UsageEvent>
{
    /// <summary>Creates the channel. With no options the defaults are used.</summary>
    public TelemetryChannel(IOptionsMonitor<TelemetryOptions>? options = null)
        : base(options?.CurrentValue ?? new TelemetryOptions())
    {
    }
}
