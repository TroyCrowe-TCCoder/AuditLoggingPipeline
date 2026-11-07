namespace AuditLoggingPipeline.Telemetry.Tests;

using System.Collections.Concurrent;
using AuditLoggingPipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal sealed class StaticOptionsMonitor(TelemetryOptions value) : IOptionsMonitor<TelemetryOptions>
{
    public TelemetryOptions CurrentValue => value;

    public TelemetryOptions Get(string? name) => value;

    public IDisposable? OnChange(Action<TelemetryOptions, string?> listener) => null;
}

internal sealed class ProviderScopeFactory(IServiceProvider provider) : IServiceScopeFactory
{
    public IServiceScope CreateScope() => provider.CreateScope();
}

internal sealed class RecordingSink : ITelemetrySink
{
    private readonly ConcurrentQueue<IReadOnlyList<UsageEvent>> _batches = new();

    public IReadOnlyList<IReadOnlyList<UsageEvent>> Batches => [.. _batches];

    public int EventCount => _batches.Sum(batch => batch.Count);

    public Task WriteBatchAsync(IReadOnlyList<UsageEvent> batch, CancellationToken cancellationToken)
    {
        _batches.Enqueue(batch);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingSink : ITelemetrySink
{
    public Task WriteBatchAsync(IReadOnlyList<UsageEvent> batch, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("sink down");
}

internal sealed class FixedUserKeyProvider(string? identifier) : IUsageUserKeyProvider
{
    public string? GetUserIdentifier() => identifier;
}

internal sealed class ThrowingUserKeyProvider : IUsageUserKeyProvider
{
    public string? GetUserIdentifier() => throw new InvalidOperationException("no user");
}

public interface IMeasuredService
{
    string Echo(string value);

    Task<int> RunAsync(bool fail, CancellationToken cancellationToken);

    Task CancelledAsync(CancellationToken cancellationToken);

    void Boom();
}

public sealed class MeasuredService : IMeasuredService
{
    public string Echo(string value) => value;

    public Task<int> RunAsync(bool fail, CancellationToken cancellationToken) =>
        fail ? Task.FromException<int>(new InvalidOperationException("boom")) : Task.FromResult(7);

    public Task CancelledAsync(CancellationToken cancellationToken) => Task.FromCanceled(new CancellationToken(true));

    public void Boom() => throw new InvalidOperationException("boom");
}

internal static class TestWait
{
    public static async Task UntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, cts.Token);
        }
    }

    public static async Task<UsageEvent> ReadAsync(TelemetryChannel channel, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        return await channel.Reader.ReadAsync(cts.Token);
    }
}
