using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuditLoggingPipeline;

/// <summary>
/// Background consumer for plugins that deliver in batches. It gathers up to
/// <see cref="BatchPipelineOptions.BatchSize"/> entries, waiting no longer than
/// <see cref="BatchPipelineOptions.FlushInterval"/> for a partly filled batch, then creates one DI
/// scope and calls every registered sink one after another inside it. Each attempt has a timeout,
/// failures are retried with exponential backoff, and a sink that still fails is logged and skipped so
/// the others still run. On shutdown it stops accepting entries and delivers what is buffered, up to
/// <see cref="EntryPipelineOptions.ShutdownDrainTimeout"/>.
/// </summary>
/// <typeparam name="TEntry">The entry type carried by the plugin.</typeparam>
/// <typeparam name="TSink">The batch sink contract resolved from DI for each batch.</typeparam>
public abstract class BatchDispatchService<TEntry, TSink> : IHostedService, IDisposable
    where TEntry : class
    where TSink : class, IBatchSink<TEntry>
{
    private readonly BoundedEntryChannel<TEntry> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;
    private readonly Func<BatchPipelineOptions> _options;
    private readonly string _pipelineName;
    private readonly CancellationTokenSource _drainTimeout = new();
    private readonly object _drainGate = new();
    private bool _disposed;
    private Task? _runTask;
    private long _lastLoggedDropCount;

    /// <summary>Creates the dispatcher.</summary>
    /// <param name="channel">The channel to drain.</param>
    /// <param name="scopeFactory">Creates the scope each batch is delivered in.</param>
    /// <param name="logger">Receives delivery failures and drop counts.</param>
    /// <param name="options">Supplies the current delivery options.</param>
    /// <param name="pipelineName">Name used in log messages, for example <c>Telemetry</c>.</param>
    protected BatchDispatchService(
        BoundedEntryChannel<TEntry> channel,
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        Func<BatchPipelineOptions> options,
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

    /// <summary>
    /// Starts reading. Like <see cref="EntryDispatchService{TEntry, TSink}"/>, the loop is not tied to the
    /// host's stopping token and is started without one, so entries buffered before a very early stop
    /// are still delivered.
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
        var reader = _channel.Reader;

        try
        {
            while (await reader.WaitToReadAsync(drainToken).ConfigureAwait(false))
            {
                var batch = await CollectBatchAsync(reader, _options(), drainToken).ConfigureAwait(false);

                if (batch.Count > 0)
                {
                    await DispatchAsync(batch, drainToken).ConfigureAwait(false);
                }

                LogNewDrops();
            }
        }
        catch (OperationCanceledException ex) when (drainToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "{Pipeline} stopped before delivering every buffered entry (the shutdown drain timeout was reached).", _pipelineName);
        }

        LogNewDrops();
    }

    private static async Task<List<TEntry>> CollectBatchAsync(ChannelReader<TEntry> reader, BatchPipelineOptions options, CancellationToken drainToken)
    {
        var size = Math.Max(1, options.BatchSize);
        var batch = new List<TEntry>(size);

        Fill(reader, batch, size);

        if (batch.Count >= size || options.FlushInterval <= TimeSpan.Zero)
        {
            return batch;
        }

        using var flush = CancellationTokenSource.CreateLinkedTokenSource(drainToken);
        flush.CancelAfter(options.FlushInterval);

        while (batch.Count < size)
        {
            var wait = reader.WaitToReadAsync(flush.Token).AsTask();
            await ((Task)wait).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            if (!wait.IsCompletedSuccessfully || !wait.Result)
            {
                break;
            }

            Fill(reader, batch, size);
        }

        return batch;
    }

    private static void Fill(ChannelReader<TEntry> reader, List<TEntry> batch, int size)
    {
        while (batch.Count < size && reader.TryRead(out var entry))
        {
            batch.Add(entry);
        }
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
        // A flag is used instead of catching ObjectDisposedException so that no logging call can fail shutdown.
        lock (_drainGate)
        {
            if (!_disposed)
            {
                _drainTimeout.CancelAfter(_options().ShutdownDrainTimeout);
            }
        }

        if (_runTask is not null)
        {
            await _runTask.WaitAsync(cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    /// <summary>Releases the drain timer.</summary>
    public void Dispose()
    {
        lock (_drainGate)
        {
            _disposed = true;
            _drainTimeout.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async Task DispatchAsync(IReadOnlyList<TEntry> batch, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        foreach (var sink in scope.ServiceProvider.GetServices<TSink>())
        {
            await WriteWithRetryAsync(sink, batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteWithRetryAsync(TSink sink, IReadOnlyList<TEntry> batch, CancellationToken cancellationToken)
    {
        var options = _options();
        var delay = options.SinkRetryDelay;
        var attempts = options.SinkMaxRetries + 1;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            if (await TryWriteAsync(sink, batch, attempt, options.SinkWriteTimeout, cancellationToken).ConfigureAwait(false))
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

        _logger.LogError("{Pipeline} sink {SinkType} could not persist a batch of {Count} entries after {Attempts} attempt(s); the batch was skipped for this sink.", _pipelineName, sink.GetType().Name, batch.Count, attempts);
    }

    private async Task<bool> TryWriteAsync(TSink sink, IReadOnlyList<TEntry> batch, int attempt, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attemptCancellation.CancelAfter(timeout);

        try
        {
            await sink.WriteBatchAsync(batch, attemptCancellation.Token).ConfigureAwait(false);
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
