namespace AuditLoggingPipeline;

using Microsoft.Extensions.Options;

/// <summary>
/// Singleton bounded channel connecting state-trail producers (the recorder and the dispatch proxy)
/// to the single consumer (<see cref="TrackerLogBackgroundService"/>). Capacity and full-mode come
/// from <see cref="StateTrailOptions"/>; lost entries are counted in <see cref="BoundedEntryChannel{TEntry}.DroppedCount"/>.
/// </summary>
public sealed class TrackerChannel : BoundedEntryChannel<StateTrailEntry>
{
	/// <summary>Creates the channel. With no options the defaults are used.</summary>
	public TrackerChannel(IOptionsMonitor<StateTrailOptions>? options = null)
		: base(options?.CurrentValue ?? new StateTrailOptions())
	{
	}
}
