namespace AuditLoggingPipeline.Sql.Tests;

using AuditLoggingPipeline;
using Microsoft.Extensions.Options;

public class SqlSinkTests
{
    [Fact]
    public async Task AuditSink_WithNullEntry_Throws()
    {
        var sink = new SqlAuditSink(new FixedOptionsMonitor<SqlAuditSinkOptions>(new SqlAuditSinkOptions { ConnectionString = "Server=x" }));

        await Assert.ThrowsAsync<ArgumentNullException>(() => sink.WriteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task StateTrailSink_WithNullEntry_Throws()
    {
        var sink = new SqlStateTrailSink(new FixedOptionsMonitor<SqlStateTrailSinkOptions>(new SqlStateTrailSinkOptions { ConnectionString = "Server=x" }));

        await Assert.ThrowsAsync<ArgumentNullException>(() => sink.WriteAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task StateTrailSink_WithUnconfiguredEntity_ThrowsWithoutConnecting()
    {
        var options = new SqlStateTrailSinkOptions { ConnectionString = "Server=invalid.invalid;Connect Timeout=1" };
        options.Targets["Users"] = "dbo.UsersAudit_Insert";
        var sink = new SqlStateTrailSink(new FixedOptionsMonitor<SqlStateTrailSinkOptions>(options));
        var entry = new StateTrailEntry { TimestampUtc = DateTime.UtcNow, EntityName = "Roles", Succeeded = true };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sink.WriteAsync(entry, CancellationToken.None));

        Assert.Contains("Roles", exception.Message);
    }

    private sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
