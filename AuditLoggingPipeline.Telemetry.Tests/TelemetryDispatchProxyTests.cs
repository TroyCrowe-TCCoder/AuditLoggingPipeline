namespace AuditLoggingPipeline.Telemetry.Tests;

using AuditLoggingPipeline;

public class TelemetryDispatchProxyTests
{
    [Fact]
    public async Task Invoke_RecordsSuccess_ForSynchronousMethod()
    {
        var channel = new TelemetryChannel();
        var proxy = CreateProxy(channel, new TelemetryOptions());

        var result = proxy.Echo("hello");

        Assert.Equal("hello", result);
        var usageEvent = await TestWait.ReadAsync(channel, CancellationToken.None);
        Assert.Equal("IMeasuredService.Echo", usageEvent.Name);
        Assert.Equal("Method", usageEvent.Category);
        Assert.Equal("IMeasuredService", usageEvent.Target);
        Assert.Equal(UsageOutcomes.Success, usageEvent.Outcome);
        Assert.NotNull(usageEvent.Duration);
    }

    [Fact]
    public async Task Invoke_RecordsSuccess_AndTrimsAsyncSuffix_ForTask()
    {
        var channel = new TelemetryChannel();
        var proxy = CreateProxy(channel, new TelemetryOptions());

        var result = await proxy.RunAsync(false, CancellationToken.None);

        Assert.Equal(7, result);
        var usageEvent = await TestWait.ReadAsync(channel, CancellationToken.None);
        Assert.Equal("IMeasuredService.Run", usageEvent.Name);
        Assert.Equal(UsageOutcomes.Success, usageEvent.Outcome);
    }

    [Fact]
    public async Task Invoke_RecordsFailureAndPropagates_WhenTaskFaults()
    {
        var channel = new TelemetryChannel();
        var proxy = CreateProxy(channel, new TelemetryOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.RunAsync(true, CancellationToken.None));

        var usageEvent = await TestWait.ReadAsync(channel, CancellationToken.None);
        Assert.Equal(UsageOutcomes.Failure, usageEvent.Outcome);
    }

    [Fact]
    public async Task Invoke_RecordsCanceled_WhenTaskIsCanceled()
    {
        var channel = new TelemetryChannel();
        var proxy = CreateProxy(channel, new TelemetryOptions());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => proxy.CancelledAsync(CancellationToken.None));

        var usageEvent = await TestWait.ReadAsync(channel, CancellationToken.None);
        Assert.Equal(UsageOutcomes.Canceled, usageEvent.Outcome);
    }

    [Fact]
    public async Task Invoke_RecordsFailureAndPropagatesOriginalException_WhenMethodThrowsSynchronously()
    {
        var channel = new TelemetryChannel();
        var proxy = CreateProxy(channel, new TelemetryOptions());

        var exception = Assert.Throws<InvalidOperationException>(proxy.Boom);

        Assert.Equal("boom", exception.Message);
        var usageEvent = await TestWait.ReadAsync(channel, CancellationToken.None);
        Assert.Equal(UsageOutcomes.Failure, usageEvent.Outcome);
    }

    [Fact]
    public void Invoke_DoesNotRecord_WhenFilterExcludesMethod()
    {
        var channel = new TelemetryChannel();
        var options = new TelemetryOptions { MethodFilter = method => method.Name != nameof(IMeasuredService.Echo) };
        var proxy = CreateProxy(channel, options);

        var result = proxy.Echo("hello");

        Assert.Equal("hello", result);
        Assert.False(channel.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Invoke_WithConfiguredCategory_UsesIt()
    {
        var channel = new TelemetryChannel();
        var proxy = CreateProxy(channel, new TelemetryOptions { ProxyCategory = "Service" });

        proxy.Echo("hello");

        Assert.Equal("Service", (await TestWait.ReadAsync(channel, CancellationToken.None)).Category);
    }

    private static IMeasuredService CreateProxy(TelemetryChannel channel, TelemetryOptions options) =>
        TelemetryDispatchProxy<IMeasuredService>.Create(
            new MeasuredService(),
            new TelemetryRecorder(channel, new StaticOptionsMonitor(options)),
            options);
}
