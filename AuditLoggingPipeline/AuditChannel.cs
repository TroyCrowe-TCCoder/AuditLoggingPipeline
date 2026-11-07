namespace AuditLoggingPipeline;

using Microsoft.Extensions.Options;

/// <summary>
/// Singleton bounded channel connecting audit producers (the recorder and the dispatch proxy) to
/// the single consumer (<see cref="AuditLogBackgroundService"/>). Capacity and full-mode come from
/// <see cref="AuditPipelineOptions"/>; lost entries are counted in <see cref="BoundedEntryChannel{TEntry}.DroppedCount"/>.
/// </summary>
public sealed class AuditChannel : BoundedEntryChannel<AuditEntry>
{
	/// <summary>Creates the channel. With no options the defaults are used.</summary>
	public AuditChannel(IOptionsMonitor<AuditPipelineOptions>? options = null)
		: base(options?.CurrentValue ?? new AuditPipelineOptions())
	{
	}
}
