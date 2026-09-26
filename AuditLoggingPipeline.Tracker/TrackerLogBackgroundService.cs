namespace AuditLoggingPipeline;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Drains the bounded <see cref="TrackerChannel"/> and fans each <see cref="StateTrailEntry"/>
/// out to every registered <see cref="ITrackerSink"/> independently. A consuming app may
/// register zero, one, or several sinks; each sink's write runs on its own isolated
/// <see cref="Task.Run(Func{Task})"/> so one sink's failure or latency never affects another
/// sink or delays the channel drain loop. Writes are entirely decoupled from the request path:
/// producers only enqueue (non-blocking,
/// <see cref="System.Threading.Channels.ChannelWriter{T}.TryWrite"/>), and any persistence
/// failure here is caught and logged, never allowed to propagate. Fully independent of
/// <c>AuditLogBackgroundService</c>: neither plugin shares state or a dispatch loop with the
/// other.
/// </summary>
public sealed class TrackerLogBackgroundService : BackgroundService
{
    private readonly TrackerChannel _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TrackerLogBackgroundService> _logger;

    private static readonly Action<ILogger, string, string, string, Exception?> LogSinkWriteFailed =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(9101, nameof(TrackerLogBackgroundService)),
            "Tracker sink {SinkType} failed to persist entry for {ServiceName}.{MethodName}.");

    /// <summary>
    /// Creates the background service that drains <paramref name="channel"/> and dispatches
    /// each entry to every registered <see cref="ITrackerSink"/>.
    /// </summary>
    public TrackerLogBackgroundService(
        TrackerChannel channel,
        IServiceScopeFactory scopeFactory,
        ILogger<TrackerLogBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);

        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Continuously drains the tracker channel and dispatches each entry to all registered
    /// sinks until <paramref name="stoppingToken"/> is cancelled.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in _channel.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            await DispatchToSinksAsync(entry, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task DispatchToSinksAsync(StateTrailEntry entry, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sinks = scope.ServiceProvider.GetServices<ITrackerSink>();

        foreach (var sink in sinks)
        {
            try
            {
                await sink.WriteAsync(entry, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSinkWriteFailed(_logger, sink.GetType().Name, entry.ServiceName, entry.MethodName, ex);
            }
        }
    }
}
