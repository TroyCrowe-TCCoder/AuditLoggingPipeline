using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditLoggingPipeline;

/// <summary>
/// Drains the <see cref="TelemetryChannel"/> in batches and delivers each batch of
/// <see cref="UsageEvent"/> to every registered <see cref="ITelemetrySink"/>. See
/// <see cref="BatchDispatchService{TEntry, TSink}"/>.
/// </summary>
public sealed class TelemetryBackgroundService : BatchDispatchService<UsageEvent, ITelemetrySink>
{
    /// <summary>Creates the background service. With no options the defaults are used.</summary>
    public TelemetryBackgroundService(
        TelemetryChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryBackgroundService> logger,
        IOptionsMonitor<TelemetryOptions>? options = null)
        : base(channel, scopeFactory, logger, () => options?.CurrentValue ?? new TelemetryOptions(), "Telemetry")
    {
    }
}
