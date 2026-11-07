namespace AuditLoggingPipeline.Telemetry.Sql.Tests;

using AuditLoggingPipeline;
using Microsoft.Extensions.Options;

public class SqlTelemetrySinkTests
{
    [Fact]
    public async Task WriteBatchAsync_WithNoEvents_DoesNotConnect()
    {
        var options = new SqlTelemetrySinkOptions { ConnectionString = "Server=invalid.invalid;Connect Timeout=1" };
        var sink = new SqlTelemetrySink(new FixedOptionsMonitor(options));

        await sink.WriteBatchAsync([], CancellationToken.None);
    }

    [Fact]
    public async Task WriteBatchAsync_WithNullEvents_Throws()
    {
        var options = new SqlTelemetrySinkOptions { ConnectionString = "Server=x" };
        var sink = new SqlTelemetrySink(new FixedOptionsMonitor(options));

        await Assert.ThrowsAsync<ArgumentNullException>(() => sink.WriteBatchAsync(null!, CancellationToken.None));
    }

    private sealed class FixedOptionsMonitor(SqlTelemetrySinkOptions value) : IOptionsMonitor<SqlTelemetrySinkOptions>
    {
        public SqlTelemetrySinkOptions CurrentValue => value;

        public SqlTelemetrySinkOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<SqlTelemetrySinkOptions, string?> listener) => null;
    }
}
