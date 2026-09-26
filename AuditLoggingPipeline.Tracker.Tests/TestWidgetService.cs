namespace AuditLoggingPipeline.Tracker.Tests;

public interface ITestWidgetService
{
    Task<string> SaveWidgetAsync(Widget widget, CancellationToken cancellationToken = default);

    Task GetWidgetAsync(string name, CancellationToken cancellationToken = default);

    Task UpdateFailingAsync(Widget widget, CancellationToken cancellationToken = default);
}

public sealed class Widget
{
    public required string Name { get; init; }

    public int Quantity { get; set; }
}

public sealed class TestWidgetService : ITestWidgetService
{
    public int SaveCallCount { get; private set; }

    public Task<string> SaveWidgetAsync(Widget widget, CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        widget.Quantity += 1;
        return Task.FromResult($"saved:{widget.Name}");
    }

    public Task GetWidgetAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateFailingAsync(Widget widget, CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");
}
