namespace AuditLoggingPipeline;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Drains the <see cref="TrackerChannel"/> and delivers each <see cref="StateTrailEntry"/> to every
/// registered <see cref="ITrackerSink"/>. Sinks run one after another inside one DI scope per entry,
/// each attempt has a timeout, failures are retried with backoff, and buffered entries are
/// delivered on shutdown. See <see cref="EntryDispatchService{TEntry, TSink}"/>.
/// </summary>
public sealed class TrackerLogBackgroundService : EntryDispatchService<StateTrailEntry, ITrackerSink>
{
	/// <summary>Creates the background service. With no options the defaults are used.</summary>
	public TrackerLogBackgroundService(
		TrackerChannel channel,
		IServiceScopeFactory scopeFactory,
		ILogger<TrackerLogBackgroundService> logger,
		IOptionsMonitor<StateTrailOptions>? options = null)
		: base(channel, scopeFactory, logger, () => options?.CurrentValue ?? new StateTrailOptions(), "StateTrail")
	{
	}

	/// <inheritdoc />
	protected override string Describe(StateTrailEntry entry) =>
		$"{entry.Action ?? entry.MethodName} {entry.EntityName}";
}
