using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuditLoggingPipeline;

/// <summary>
/// Background consumer shared by every plugin. For each drained entry it creates one DI scope and
/// calls every registered sink one after another inside it, so scoped dependencies such as a
/// database connection are never used by two sinks at once. Each attempt has a timeout, failures
/// are retried with exponential backoff, and a sink that still fails is logged and skipped so the
/// others still run. On shutdown it stops accepting entries and delivers what is buffered, up to
/// <see cref="EntryPipelineOptions.ShutdownDrainTimeout"/>.
/// </summary>
/// <typeparam name="TEntry">The entry type carried by the plugin.</typeparam>
/// <typeparam name="TSink">The sink contract resolved from DI for each entry.</typeparam>
public abstract class EntryDispatchService<TEntry, TSink> : IHostedService, IDisposable
	where TEntry : class
	where TSink : class, IEntrySink<TEntry>
{
	private readonly BoundedEntryChannel<TEntry> _channel;
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger _logger;
	private readonly Func<EntryPipelineOptions> _options;
	private readonly string _pipelineName;
	private readonly CancellationTokenSource _drainTimeout = new();
	private Task? _runTask;
	private long _lastLoggedDropCount;

	/// <summary>Creates the dispatcher.</summary>
	/// <param name="channel">The channel to drain.</param>
	/// <param name="scopeFactory">Creates the scope each entry is delivered in.</param>
	/// <param name="logger">Receives delivery failures and drop counts.</param>
	/// <param name="options">Supplies the current delivery options.</param>
	/// <param name="pipelineName">Name used in log messages, for example <c>Audit</c>.</param>
	protected EntryDispatchService(
		BoundedEntryChannel<TEntry> channel,
		IServiceScopeFactory scopeFactory,
		ILogger logger,
		Func<EntryPipelineOptions> options,
		string pipelineName)
	{
		ArgumentNullException.ThrowIfNull(channel);
		ArgumentNullException.ThrowIfNull(scopeFactory);
		ArgumentNullException.ThrowIfNull(logger);
		ArgumentNullException.ThrowIfNull(options);
		ArgumentException.ThrowIfNullOrWhiteSpace(pipelineName);

		_channel = channel;
		_scopeFactory = scopeFactory;
		_logger = logger;
		_options = options;
		_pipelineName = pipelineName;
	}

	/// <summary>A short description of an entry for log messages, for example its action or method.</summary>
	protected abstract string Describe(TEntry entry);

	/// <summary>
	/// Starts reading. The read loop is not tied to the host's stopping token, and it is started
	/// without a token, so entries buffered before a very early stop are still delivered. The drain
	/// timeout started by <see cref="StopAsync"/> bounds the shutdown instead.
	/// </summary>
	/// <param name="cancellationToken">Not used; the loop runs until <see cref="StopAsync"/>.</param>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		var drainToken = _drainTimeout.Token;
		_runTask ??= Task.Run(() => RunAsync(drainToken), CancellationToken.None);

		return Task.CompletedTask;
	}

	private async Task RunAsync(CancellationToken drainToken)
	{
		try
		{
			await foreach (var entry in _channel.ReadAllAsync(drainToken).ConfigureAwait(false))
			{
				await DispatchAsync(entry, drainToken).ConfigureAwait(false);
				LogNewDrops();
			}
		}
		catch (OperationCanceledException ex) when (drainToken.IsCancellationRequested)
		{
			_logger.LogError(ex, "{Pipeline} stopped before delivering every buffered entry (the shutdown drain timeout was reached).", _pipelineName);
		}

		LogNewDrops();
	}

	/// <summary>
	/// Stops accepting entries and waits for the buffered ones to be delivered, up to the drain
	/// timeout or until <paramref name="cancellationToken"/> is cancelled.
	/// </summary>
	/// <param name="cancellationToken">Signals that the host no longer wants a graceful shutdown.</param>
	public async Task StopAsync(CancellationToken cancellationToken)
	{
		_channel.Complete();

		// A host can dispose its services before stopping them; the drain then runs without the timeout.
		try
		{
			_drainTimeout.CancelAfter(_options().ShutdownDrainTimeout);
		}
		catch (ObjectDisposedException ex)
		{
			_logger.LogError(ex, "{Pipeline} was stopped after it was disposed; the buffered entries are delivered without a drain timeout.", _pipelineName);
		}

		if (_runTask is not null)
		{
			await _runTask.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
		}
	}

	/// <summary>Releases the drain timer.</summary>
	public void Dispose()
	{
		_drainTimeout.Dispose();
		GC.SuppressFinalize(this);
	}

	private async Task DispatchAsync(TEntry entry, CancellationToken cancellationToken)
	{
		using var scope = _scopeFactory.CreateScope();

		foreach (var sink in scope.ServiceProvider.GetServices<TSink>())
		{
			await WriteWithRetryAsync(sink, entry, cancellationToken).ConfigureAwait(false);
		}
	}

	private async Task WriteWithRetryAsync(TSink sink, TEntry entry, CancellationToken cancellationToken)
	{
		var options = _options();
		var delay = options.SinkRetryDelay;
		var attempts = options.SinkMaxRetries + 1;

		for (var attempt = 1; attempt <= attempts; attempt++)
		{
			if (await TryWriteAsync(sink, entry, attempt, options.SinkWriteTimeout, cancellationToken).ConfigureAwait(false))
			{
				return;
			}

			cancellationToken.ThrowIfCancellationRequested();

			if (attempt < attempts)
			{
				await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
				delay += delay;
			}
		}

		_logger.LogError("{Pipeline} sink {SinkType} could not persist {Entry} after {Attempts} attempt(s); the entry was skipped for this sink.", _pipelineName, sink.GetType().Name, Describe(entry), attempts);
	}

	private async Task<bool> TryWriteAsync(TSink sink, TEntry entry, int attempt, TimeSpan timeout, CancellationToken cancellationToken)
	{
		using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		attemptCancellation.CancelAfter(timeout);

		try
		{
			await sink.WriteAsync(entry, attemptCancellation.Token).ConfigureAwait(false);
			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "{Pipeline} sink {SinkType} failed on attempt {Attempt}.", _pipelineName, sink.GetType().Name, attempt);
			return false;
		}
	}

	private void LogNewDrops()
	{
		var dropped = _channel.DroppedCount;
		if (dropped != _lastLoggedDropCount)
		{
			_lastLoggedDropCount = dropped;
			_logger.LogError("{Pipeline} has lost {DroppedCount} entries in total because its buffer was full or closed.", _pipelineName, dropped);
		}
	}
}
