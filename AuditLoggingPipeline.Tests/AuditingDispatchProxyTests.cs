namespace AuditLoggingPipeline.Tests;

public class AuditingDispatchProxyTests
{
    [Fact]
    public async Task Invoke_CallsUnderlyingMethodExactlyOnce_ForAuditedMethod()
    {
        var real = new TestService();
        var channel = new AuditChannel();
        var proxy = AuditingDispatchProxy<ITestService>.Create(real, channel, httpContextAccessor: null);

        var result = await proxy.SaveWidgetAsync("widget-1");

        Assert.Equal("saved:widget-1", result);
        Assert.Equal(1, real.SaveCallCount);
    }

    [Fact]
    public async Task Invoke_EnqueuesAuditEntry_ForAuditedMethodOnSuccess()
    {
        var real = new TestService();
        var channel = new AuditChannel();
        var proxy = AuditingDispatchProxy<ITestService>.Create(real, channel, httpContextAccessor: null);

        await proxy.SaveWidgetAsync("widget-1");

        var entry = await WaitForEntryAsync(channel);

        Assert.Equal("SaveWidgetAsync", entry.MethodName);
        Assert.True(entry.Succeeded);
        Assert.Null(entry.ErrorMessage);
    }

    [Fact]
    public async Task Invoke_EnqueuesFailedAuditEntry_WhenAuditedMethodThrows()
    {
        var real = new TestService();
        var channel = new AuditChannel();
        var proxy = AuditingDispatchProxy<ITestService>.Create(real, channel, httpContextAccessor: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.UpdateFailingAsync());

        var entry = await WaitForEntryAsync(channel);

        Assert.Equal("UpdateFailingAsync", entry.MethodName);
        Assert.False(entry.Succeeded);
        Assert.Equal("boom", entry.ErrorMessage);
    }

    [Fact]
    public async Task Invoke_DoesNotEnqueueAuditEntry_ForNonAuditedMethod()
    {
        var real = new TestService();
        var channel = new AuditChannel();
        var proxy = AuditingDispatchProxy<ITestService>.Create(real, channel, httpContextAccessor: null);

        await proxy.GetWidgetAsync("widget-1");

        // Give any (incorrect) background enqueue a moment to occur, then confirm nothing arrived.
        await Task.Delay(50);
        var reader = channel.Reader;
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public void Create_ThrowsArgumentNullException_WhenDecoratedIsNull()
    {
        var channel = new AuditChannel();

        Assert.Throws<ArgumentNullException>(() =>
            AuditingDispatchProxy<ITestService>.Create(null!, channel, httpContextAccessor: null));
    }

    [Fact]
    public void Create_ThrowsArgumentNullException_WhenChannelIsNull()
    {
        var real = new TestService();

        Assert.Throws<ArgumentNullException>(() =>
            AuditingDispatchProxy<ITestService>.Create(real, null!, httpContextAccessor: null));
    }

    private static async Task<AuditEntry> WaitForEntryAsync(AuditChannel channel, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        return await channel.Reader.ReadAsync(cts.Token);
    }
}
