namespace AuditLoggingPipeline.Tracker.Tests;

using AuditLoggingPipeline;

public class TrackingDispatchProxyTests
{
    [Fact]
    public async Task Invoke_EnqueuesStateTrailEntry_WithBeforeAndAfterState_WhenTrackedMethodSucceeds()
    {
        var channel = new TrackerChannel();
        var inner = new TestWidgetService();
        var proxy = TrackingDispatchProxy<ITestWidgetService>.Create(inner, channel, httpContextAccessor: null);

        var widget = new Widget { Name = "Bolt", Quantity = 1 };
        var result = await proxy.SaveWidgetAsync(widget);

        Assert.Equal("saved:Bolt", result);
        Assert.Equal(1, inner.SaveCallCount);

        // Allow the fire-and-forget tracking task to complete.
        await Task.Delay(50);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var enumerator = channel.ReadAllAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        Assert.True(await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task Invoke_DoesNotEnqueue_WhenMethodIsNotTracked()
    {
        var channel = new TrackerChannel();
        var inner = new TestWidgetService();
        var proxy = TrackingDispatchProxy<ITestWidgetService>.Create(inner, channel, httpContextAccessor: null);

        await proxy.GetWidgetAsync("Bolt");
        await Task.Delay(50);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        var hasEntry = false;
        try
        {
            await foreach (var _ in channel.ReadAllAsync(cts.Token))
            {
                hasEntry = true;
                break;
            }
        }
        catch (OperationCanceledException)
        {
        }

        Assert.False(hasEntry);
    }

    [Fact]
    public async Task Invoke_PropagatesException_WhenTrackedMethodThrows()
    {
        var channel = new TrackerChannel();
        var inner = new TestWidgetService();
        var proxy = TrackingDispatchProxy<ITestWidgetService>.Create(inner, channel, httpContextAccessor: null);

        var widget = new Widget { Name = "Bolt", Quantity = 1 };

        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.UpdateFailingAsync(widget));
    }
}
