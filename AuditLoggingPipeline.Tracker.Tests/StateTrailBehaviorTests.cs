namespace AuditLoggingPipeline.Tracker.Tests;

using AuditLoggingPipeline;

public class StateTrailBehaviorTests
{
    [Fact]
    public async Task Invoke_RecordsAfterStateAtCallTime_AndLeavesBeforeStateEmpty()
    {
        var channel = new TrackerChannel();
        var options = new StateTrailOptions { EntityIdResolver = entity => ((Widget)entity).Name };
        var proxy = TrackingDispatchProxy<ITestWidgetService>.Create(new TestWidgetService(), channel, new FixedProvider(), options);

        await proxy.SaveWidgetAsync(new Widget { Name = "Bolt", Quantity = 1 });

        var entry = await ReadEntryAsync(channel, CancellationToken.None);

        Assert.Equal("SaveWidget", entry.Action);
        Assert.Equal("Widget", entry.EntityName);
        Assert.Equal("Bolt", entry.EntityId);
        Assert.Null(entry.BeforeState);
        Assert.Contains("\"Quantity\":1", entry.AfterState);
        Assert.Equal("Pat Example", entry.ActorDisplayName);
        Assert.True(entry.Succeeded);
    }

    [Fact]
    public async Task Invoke_RecordsFailure_WhenTrackedMethodThrows()
    {
        var channel = new TrackerChannel();
        var proxy = TrackingDispatchProxy<ITestWidgetService>.Create(new TestWidgetService(), channel, new FixedProvider(), new StateTrailOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.UpdateFailingAsync(new Widget { Name = "Bolt" }));

        var entry = await ReadEntryAsync(channel, CancellationToken.None);

        Assert.False(entry.Succeeded);
        Assert.Equal("boom", entry.ErrorMessage);
    }

    [Fact]
    public void Record_SerializesBothStates_AndJoinsKeysIntoEntityId()
    {
        var channel = new TrackerChannel();
        var recorder = new StateTrailRecorder(channel, new FixedProvider());

        var recorded = recorder.Record(new StateTrailRecord
        {
            Action = "UserRole.Upsert",
            EntityName = "UserRole",
            EntityKeys = new Dictionary<string, string> { ["UserId"] = "5", ["RoleId"] = "9" },
            BeforeState = new Widget { Name = "Old", Quantity = 1 },
            AfterState = new Widget { Name = "New", Quantity = 2 },
        });

        Assert.True(recorded);
        Assert.True(channel.Reader.TryRead(out var entry));
        Assert.Equal("5|9", entry.EntityId);
        Assert.Contains("Old", entry.BeforeState);
        Assert.Contains("New", entry.AfterState);
        Assert.Equal("Pat Example", entry.ActorDisplayName);
    }

    private static async Task<StateTrailEntry> ReadEntryAsync(TrackerChannel channel, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        return await channel.Reader.ReadAsync(cts.Token);
    }

    private sealed class FixedProvider : IAuditContextProvider
    {
        public AuditContext GetContext() => new() { ActorDisplayName = "Pat Example" };
    }
}
