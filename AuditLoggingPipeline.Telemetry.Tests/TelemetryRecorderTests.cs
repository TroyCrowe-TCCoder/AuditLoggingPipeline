namespace AuditLoggingPipeline.Telemetry.Tests;

using System.Security.Cryptography;
using System.Text;
using AuditLoggingPipeline;

public class TelemetryRecorderTests
{
    [Fact]
    public void Record_WithOptions_StampsApplicationVersionEnvironmentAndTimestamp()
    {
        var channel = new TelemetryChannel();
        var options = new TelemetryOptions { ApplicationName = "Identity", ApplicationVersion = "1.2.3", EnvironmentName = "Test" };
        var recorder = new TelemetryRecorder(channel, new StaticOptionsMonitor(options));
        var before = DateTime.UtcNow;

        var recorded = recorder.Record(new UsageRecord
        {
            Name = "Report.Export",
            Category = "Report",
            Target = "Export",
            Duration = TimeSpan.FromMilliseconds(42),
            Properties = new Dictionary<string, string> { ["format"] = "csv" },
        });

        Assert.True(recorded);
        Assert.True(channel.Reader.TryRead(out var usageEvent));
        Assert.Equal("Identity", usageEvent.Application);
        Assert.Equal("1.2.3", usageEvent.Version);
        Assert.Equal("Test", usageEvent.Environment);
        Assert.Equal("Report.Export", usageEvent.Name);
        Assert.Equal("Report", usageEvent.Category);
        Assert.Equal("Export", usageEvent.Target);
        Assert.Equal(TimeSpan.FromMilliseconds(42), usageEvent.Duration);
        Assert.Equal(UsageOutcomes.Success, usageEvent.Outcome);
        Assert.Equal("csv", usageEvent.Properties!["format"]);
        Assert.Equal(1, usageEvent.SampleWeight);
        Assert.Null(usageEvent.UserKey);
        Assert.True(usageEvent.TimestampUtc >= before);
    }

    [Fact]
    public async Task RecordAsync_WithRecord_EnqueuesEvent()
    {
        var channel = new TelemetryChannel();
        var recorder = new TelemetryRecorder(channel);

        var recorded = await recorder.RecordAsync(new UsageRecord { Name = "Login" }, CancellationToken.None);

        Assert.True(recorded);
        Assert.Equal("Login", (await TestWait.ReadAsync(channel, CancellationToken.None)).Name);
    }

    [Fact]
    public void Record_KeepsAndWeightsSuccesses_WhenSampling()
    {
        var channel = new TelemetryChannel();
        var recorder = new TelemetryRecorder(channel, new StaticOptionsMonitor(new TelemetryOptions { SampleRate = 0.5 }));

        for (var index = 0; index < 1000; index++)
        {
            recorder.Record(new UsageRecord { Name = "Hit" });
        }

        var kept = 0;
        while (channel.Reader.TryRead(out var usageEvent))
        {
            kept++;
            Assert.Equal(2, usageEvent.SampleWeight);
        }

        Assert.InRange(kept, 350, 650);
    }

    [Theory]
    [InlineData(UsageOutcomes.Failure)]
    [InlineData(UsageOutcomes.Canceled)]
    public void Record_AlwaysKeepsNonSuccessEvents_WhenSampling(string outcome)
    {
        var channel = new TelemetryChannel();
        var recorder = new TelemetryRecorder(channel, new StaticOptionsMonitor(new TelemetryOptions { SampleRate = 0.0001 }));

        for (var index = 0; index < 50; index++)
        {
            Assert.True(recorder.Record(new UsageRecord { Name = "Bad", Outcome = outcome }));
        }

        var kept = 0;
        while (channel.Reader.TryRead(out var usageEvent))
        {
            kept++;
            Assert.Equal(1, usageEvent.SampleWeight);
        }

        Assert.Equal(50, kept);
    }

    [Fact]
    public void Record_HashesUserIdentifierWithSalt_WhenEnabled()
    {
        var channel = new TelemetryChannel();
        var options = new TelemetryOptions { IncludeUserKey = true, UserKeySalt = "pepper" };
        var recorder = new TelemetryRecorder(channel, new StaticOptionsMonitor(options), new FixedUserKeyProvider("pat@example.com"));

        recorder.Record(new UsageRecord { Name = "Hit" });

        Assert.True(channel.Reader.TryRead(out var usageEvent));
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("pepper"), Encoding.UTF8.GetBytes("pat@example.com")));
        Assert.Equal(expected, usageEvent.UserKey);
        Assert.DoesNotContain("pat@example.com", usageEvent.UserKey);
    }

    [Fact]
    public void Record_OmitsUserKey_WhenDisabled()
    {
        var channel = new TelemetryChannel();
        var options = new TelemetryOptions { IncludeUserKey = false, UserKeySalt = "pepper" };
        var recorder = new TelemetryRecorder(channel, new StaticOptionsMonitor(options), new FixedUserKeyProvider("pat@example.com"));

        recorder.Record(new UsageRecord { Name = "Hit" });

        Assert.True(channel.Reader.TryRead(out var usageEvent));
        Assert.Null(usageEvent.UserKey);
    }

    [Fact]
    public void Record_StillRecords_WhenUserKeyProviderThrows()
    {
        var channel = new TelemetryChannel();
        var options = new TelemetryOptions { IncludeUserKey = true, UserKeySalt = "pepper" };
        var recorder = new TelemetryRecorder(channel, new StaticOptionsMonitor(options), new ThrowingUserKeyProvider());

        var recorded = recorder.Record(new UsageRecord { Name = "Hit" });

        Assert.True(recorded);
        Assert.True(channel.Reader.TryRead(out var usageEvent));
        Assert.Null(usageEvent.UserKey);
    }

    [Fact]
    public void StartTimer_RecordsDurationAndOutcome_OnDispose()
    {
        var channel = new TelemetryChannel();
        var recorder = new TelemetryRecorder(channel);

        using (var timer = recorder.StartTimer("Import", "Data", "Csv"))
        {
            timer.Fail();
        }

        Assert.True(channel.Reader.TryRead(out var usageEvent));
        Assert.Equal("Import", usageEvent.Name);
        Assert.Equal("Data", usageEvent.Category);
        Assert.Equal("Csv", usageEvent.Target);
        Assert.Equal(UsageOutcomes.Failure, usageEvent.Outcome);
        Assert.NotNull(usageEvent.Duration);
    }

    [Fact]
    public void StartTimer_RecordsOnce_WhenDisposedTwice()
    {
        var channel = new TelemetryChannel();
        var recorder = new TelemetryRecorder(channel);

        var timer = recorder.StartTimer("Import");
        timer.Dispose();
        timer.Dispose();

        Assert.True(channel.Reader.TryRead(out _));
        Assert.False(channel.Reader.TryRead(out _));
    }
}
