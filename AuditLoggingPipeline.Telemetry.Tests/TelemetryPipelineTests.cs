namespace AuditLoggingPipeline.Telemetry.Tests;

using AuditLoggingPipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public class TelemetryPipelineTests
{
    [Fact]
    public async Task Service_DeliversFullBatch_WithoutWaitingForFlushInterval()
    {
        var (service, channel, sink) = Create(new TelemetryOptions { BatchSize = 3, FlushInterval = TimeSpan.FromMinutes(5) });
        using var disposable = service;
        await service.StartAsync(CancellationToken.None);

        for (var index = 0; index < 3; index++)
        {
            channel.TryWrite(new UsageEvent { Name = $"E{index}" });
        }

        await TestWait.UntilAsync(() => sink.EventCount == 3, CancellationToken.None);
        Assert.Single(sink.Batches);
        Assert.Equal(3, sink.Batches[0].Count);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Service_FlushesPartialBatch_AfterFlushInterval()
    {
        var (service, channel, sink) = Create(new TelemetryOptions { BatchSize = 100, FlushInterval = TimeSpan.FromMilliseconds(100) });
        using var disposable = service;
        await service.StartAsync(CancellationToken.None);

        channel.TryWrite(new UsageEvent { Name = "A" });
        channel.TryWrite(new UsageEvent { Name = "B" });

        await TestWait.UntilAsync(() => sink.EventCount == 2, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Service_DrainsBufferedEvents_OnStop()
    {
        var (service, channel, sink) = Create(new TelemetryOptions { BatchSize = 100, FlushInterval = TimeSpan.FromMinutes(5) });
        using var disposable = service;
        await service.StartAsync(CancellationToken.None);

        for (var index = 0; index < 5; index++)
        {
            channel.TryWrite(new UsageEvent { Name = $"E{index}" });
        }

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(5, sink.EventCount);
    }

    [Fact]
    public async Task Service_StopAfterDispose_DoesNotThrowAndStillDrains()
    {
        var (service, channel, sink) = Create(new TelemetryOptions { BatchSize = 100, FlushInterval = TimeSpan.FromMinutes(5) });
        await service.StartAsync(CancellationToken.None);
        channel.TryWrite(new UsageEvent { Name = "A" });
        service.Dispose();

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, sink.EventCount);
    }

    [Fact]
    public async Task Service_DeliversToHealthySink_WhenAnotherSinkFails()
    {
        var options = new TelemetryOptions { BatchSize = 2, FlushInterval = TimeSpan.FromMilliseconds(50), SinkMaxRetries = 0 };
        var healthy = new RecordingSink();
        var (service, channel, _) = Create(options, new ThrowingSink(), healthy);
        using var disposable = service;
        await service.StartAsync(CancellationToken.None);

        channel.TryWrite(new UsageEvent { Name = "A" });
        channel.TryWrite(new UsageEvent { Name = "B" });

        await TestWait.UntilAsync(() => healthy.EventCount == 2, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AddTelemetry_WithScopedProxy_RecordsMeasuredCalls()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTelemetry(options => options.ApplicationName = "Identity");
        services.AddTelemetryScoped<IMeasuredService, MeasuredService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var measured = scope.ServiceProvider.GetRequiredService<IMeasuredService>();
        measured.Echo("hello");

        var channel = provider.GetRequiredService<TelemetryChannel>();
        var usageEvent = await TestWait.ReadAsync(channel, CancellationToken.None);
        Assert.Equal("Identity", usageEvent.Application);
        Assert.Equal("IMeasuredService.Echo", usageEvent.Name);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(1.5)]
    public void AddTelemetry_WithInvalidSampleRate_RejectsOptions(double sampleRate)
    {
        var services = new ServiceCollection();
        services.AddTelemetry(options => options.SampleRate = sampleRate);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TelemetryOptions>>().Value);
    }

    [Fact]
    public void AddTelemetry_RequiresSalt_WhenUserKeyIsEnabled()
    {
        var services = new ServiceCollection();
        services.AddTelemetry(options => options.IncludeUserKey = true);
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<TelemetryOptions>>().Value);
    }

    private static (TelemetryBackgroundService Service, TelemetryChannel Channel, RecordingSink Sink) Create(
        TelemetryOptions options,
        params ITelemetrySink[] extraSinks)
    {
        var sink = new RecordingSink();
        var services = new ServiceCollection();
        services.AddSingleton<ITelemetrySink>(sink);
        foreach (var extra in extraSinks)
        {
            services.AddSingleton(extra);
        }

        var provider = services.BuildServiceProvider();
        var channel = new TelemetryChannel(new StaticOptionsMonitor(options));
        var service = new TelemetryBackgroundService(
            channel,
            new ProviderScopeFactory(provider),
            NullLogger<TelemetryBackgroundService>.Instance,
            new StaticOptionsMonitor(options));

        return (service, channel, sink);
    }
}
