namespace AuditLoggingPipeline.Tests;

/// <summary>
/// Simple test interface following the Save/Add/Update/Archive/Delete audited-method naming
/// convention plus a non-audited Get method, used to exercise <see cref="AuditingDispatchProxy{TInterface}"/>.
/// </summary>
public interface ITestService
{
    Task<string> SaveWidgetAsync(string name, CancellationToken cancellationToken = default);

    Task GetWidgetAsync(string name, CancellationToken cancellationToken = default);

    Task UpdateFailingAsync(CancellationToken cancellationToken = default);
}

public sealed class TestService : ITestService
{
    public int SaveCallCount { get; private set; }

    public Task<string> SaveWidgetAsync(string name, CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        return Task.FromResult($"saved:{name}");
    }

    public Task GetWidgetAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task UpdateFailingAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("boom");
}
