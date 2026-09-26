namespace AuditLoggingPipeline.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

public class AuditLogBackgroundServiceTests
{
    [Fact]
    public async Task ExecuteAsync_DispatchesEntry_ToAllRegisteredSinks()
    {
        var channel = new AuditChannel();
        var sinkA = new RecordingAuditSink();
        var sinkB = new RecordingAuditSink();

        var services = new ServiceCollection();
        services.AddSingleton<IAuditSink>(sinkA);
        services.AddSingleton<IAuditSink>(sinkB);
        var provider = services.BuildServiceProvider();

        var service = new AuditLogBackgroundService(
            channel,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuditLogBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();
        var runTask = service.StartAsync(cts.Token);

        channel.TryWrite(new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            ServiceName = "ITestService",
            MethodName = "SaveWidgetAsync",
            Succeeded = true,
        });

        await WaitForConditionAsync(() => sinkA.Received.Count == 1 && sinkB.Received.Count == 1);

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        Assert.Single(sinkA.Received);
        Assert.Single(sinkB.Received);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesToOtherSinks_WhenOneSinkThrows()
    {
        var channel = new AuditChannel();
        var throwing = new ThrowingAuditSink();
        var recording = new RecordingAuditSink();

        var services = new ServiceCollection();
        services.AddSingleton<IAuditSink>(throwing);
        services.AddSingleton<IAuditSink>(recording);
        var provider = services.BuildServiceProvider();

        var service = new AuditLogBackgroundService(
            channel,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuditLogBackgroundService>.Instance);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        channel.TryWrite(new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            ServiceName = "ITestService",
            MethodName = "SaveWidgetAsync",
            Succeeded = true,
        });

        await WaitForConditionAsync(() => recording.Received.Count == 1);

        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        Assert.Single(recording.Received);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNullException_WhenChannelIsNull()
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentNullException>(() => new AuditLogBackgroundService(
            null!,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuditLogBackgroundService>.Instance));
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }
}
