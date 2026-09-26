namespace AuditLoggingPipeline.Tests;

using System.Threading.Channels;

public class AuditChannelTests
{
    [Fact]
    public void TryWrite_ReturnsTrue_WhenChannelHasCapacity()
    {
        var channel = new AuditChannel();
        var entry = CreateEntry();

        var result = channel.TryWrite(entry);

        Assert.True(result);
    }

    [Fact]
    public async Task Reader_ReceivesEnqueuedEntry()
    {
        var channel = new AuditChannel();
        var entry = CreateEntry();

        channel.TryWrite(entry);

        var received = await channel.Reader.ReadAsync();

        Assert.Same(entry, received);
    }

    private static AuditEntry CreateEntry() => new()
    {
        TimestampUtc = DateTime.UtcNow,
        ServiceName = "ITestService",
        MethodName = "SaveWidgetAsync",
        Succeeded = true,
    };
}
