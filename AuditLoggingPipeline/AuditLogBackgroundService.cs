namespace AuditLoggingPipeline;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Drains the <see cref="AuditChannel"/> and delivers each <see cref="AuditEntry"/> to every
/// registered <see cref="IAuditSink"/>. Sinks run one after another inside one DI scope per entry,
/// each attempt has a timeout, failures are retried with backoff, and buffered entries are
/// delivered on shutdown. See <see cref="EntryDispatchService{TEntry, TSink}"/>.
/// </summary>
public sealed class AuditLogBackgroundService : EntryDispatchService<AuditEntry, IAuditSink>
{
	/// <summary>Creates the background service. With no options the defaults are used.</summary>
	public AuditLogBackgroundService(
		AuditChannel channel,
		IServiceScopeFactory scopeFactory,
		ILogger<AuditLogBackgroundService> logger,
		IOptionsMonitor<AuditPipelineOptions>? options = null)
		: base(channel, scopeFactory, logger, () => options?.CurrentValue ?? new AuditPipelineOptions(), "Audit")
	{
	}

	/// <inheritdoc />
	protected override string Describe(AuditEntry entry) =>
		entry.Action ?? $"{entry.ServiceName}.{entry.MethodName}";
}
