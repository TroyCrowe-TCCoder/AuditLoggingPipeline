namespace AuditLoggingPipeline.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

public class CoreBehaviorTests
{
    [Theory]
    [InlineData("SaveWidgetAsync", true)]
    [InlineData("UpsertRoleAsync", true)]
    [InlineData("DeleteUser", true)]
    [InlineData("Save", true)]
    [InlineData("GetWidgetAsync", false)]
    [InlineData("Saved", false)]
    [InlineData("Updater", false)]
    public void IsMatch_UsesPrefixesWithWordBoundary(string methodName, bool expected)
    {
        Assert.Equal(expected, InterceptionMatcher.IsMatch(new AuditPipelineOptions(), FakeMethod(methodName)));
    }

    [Fact]
    public void IsMatch_MatchesConfiguredSuffix()
    {
        var options = new AuditPipelineOptions { MethodSuffixes = { "Onboarding" } };

        Assert.True(InterceptionMatcher.IsMatch(options, FakeMethod("UserOrganizationMapOnboardingAsync")));
        Assert.False(InterceptionMatcher.IsMatch(new AuditPipelineOptions(), FakeMethod("UserOrganizationMapOnboardingAsync")));
    }

    [Fact]
    public void IsMatch_PrefersCustomMatcher()
    {
        var options = new AuditPipelineOptions { MethodMatcher = method => method.Name == "GetWidgetAsync" };

        Assert.True(InterceptionMatcher.IsMatch(options, FakeMethod("GetWidgetAsync")));
        Assert.False(InterceptionMatcher.IsMatch(options, FakeMethod("SaveWidgetAsync")));
    }

    [Fact]
    public void JsonPayload_ReturnsNull_ForNull()
    {
        Assert.Null(JsonPayload.Serialize(null));
    }

    [Fact]
    public void JsonPayload_PassesThrough_ValidJsonStrings()
    {
        const string json = "{\"Name\":\"A\"}";

        Assert.Equal(json, JsonPayload.Serialize(json));
    }

    [Fact]
    public void JsonPayload_QuotesPlainStrings()
    {
        Assert.Equal("\"hello\"", JsonPayload.Serialize("hello"));
    }

    [Fact]
    public void JsonPayload_DoesNotTruncateLargeValues()
    {
        var json = JsonPayload.Serialize(new { Text = new string('x', 50_000) });

        Assert.NotNull(json);
        using var document = System.Text.Json.JsonDocument.Parse(json!);
        Assert.Equal(50_000, document.RootElement.GetProperty("Text").GetString()!.Length);
    }

    [Fact]
    public void Recorder_CapturesContext_AndCopiesRecordFields()
    {
        var channel = new AuditChannel();
        var provider = new FixedContextProvider(new AuditContext
        {
            ActorId = "u1",
            ActorDisplayName = "Pat Example",
            IpAddress = "10.0.0.1",
            CorrelationId = "corr-1",
        });
        var recorder = new AuditRecorder(channel, provider);

        Assert.True(recorder.Record(new AuditRecord { Action = "Role.Upsert", EntityType = "Role", EntityId = "7", Reason = "why" }));
        Assert.True(channel.Reader.TryRead(out var entry));

        Assert.Equal("Role.Upsert", entry.Action);
        Assert.Equal("Role", entry.EntityType);
        Assert.Equal("7", entry.EntityId);
        Assert.Equal("Pat Example", entry.ActorDisplayName);
        Assert.Equal("10.0.0.1", entry.IpAddress);
        Assert.Equal("corr-1", entry.CorrelationId);
        Assert.Equal("why", entry.Reason);
        Assert.True(entry.Succeeded);
    }

    [Fact]
    public void Recorder_WithEntityKeysAndNoEntityId_JoinsKeysIntoEntityId()
    {
        var channel = new AuditChannel();
        var recorder = new AuditRecorder(channel, new FixedContextProvider(new AuditContext { ActorId = "u1" }));
        var keys = new Dictionary<string, string> { ["UserId"] = "u1", ["RoleId"] = "r1" };

        Assert.True(recorder.Record(new AuditRecord { Action = "UserRoleInsert", EntityType = "UserRole", EntityKeys = keys }));
        Assert.True(channel.Reader.TryRead(out var entry));

        Assert.Equal("u1|r1", entry.EntityId);
        Assert.Same(keys, entry.EntityKeys);
    }

    [Fact]
    public void Recorder_WithEntityIdAndKeys_KeepsExplicitEntityId()
    {
        var channel = new AuditChannel();
        var recorder = new AuditRecorder(channel, new FixedContextProvider(new AuditContext { ActorId = "u1" }));
        var keys = new Dictionary<string, string> { ["RoleId"] = "r1" };

        Assert.True(recorder.Record(new AuditRecord { Action = "RoleUpsert", EntityType = "Role", EntityId = "custom", EntityKeys = keys }));
        Assert.True(channel.Reader.TryRead(out var entry));

        Assert.Equal("custom", entry.EntityId);
    }

    [Fact]
    public void Channel_CountsDroppedEntries_WhenFull()
    {
        var channel = new AuditChannel(Monitor(new AuditPipelineOptions { ChannelCapacity = 2 }));

        for (var i = 0; i < 5; i++)
        {
            channel.TryWrite(NewEntry());
        }

        Assert.Equal(3, channel.DroppedCount);
    }

    [Fact]
    public async Task Dispatch_RetriesFailedSink_UntilItSucceeds()
    {
        var sink = new FlakySink(failures: 2);
        var (channel, service) = Build(sink, new AuditPipelineOptions { SinkMaxRetries = 3, SinkRetryDelay = TimeSpan.FromMilliseconds(5) });

        await service.StartAsync(CancellationToken.None);
        channel.TryWrite(NewEntry());
        await WaitForAsync(() => sink.Received == 1, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1, sink.Received);
        Assert.Equal(3, sink.Attempts);
    }

    [Fact]
    public async Task Dispatch_GivesUp_AfterMaxRetries_AndKeepsProcessing()
    {
        var sink = new FlakySink(failures: int.MaxValue);
        var (channel, service) = Build(sink, new AuditPipelineOptions { SinkMaxRetries = 1, SinkRetryDelay = TimeSpan.FromMilliseconds(5) });

        await service.StartAsync(CancellationToken.None);
        channel.TryWrite(NewEntry());
        channel.TryWrite(NewEntry());
        await WaitForAsync(() => sink.Attempts == 4, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(4, sink.Attempts);
        Assert.Equal(0, sink.Received);
    }

    [Fact]
    public async Task Dispatch_TimesOutSlowSink()
    {
        var sink = new FlakySink(failures: 0) { Delay = TimeSpan.FromSeconds(30) };
        var (channel, service) = Build(sink, new AuditPipelineOptions
        {
            SinkMaxRetries = 0,
            SinkWriteTimeout = TimeSpan.FromMilliseconds(50),
        });

        await service.StartAsync(CancellationToken.None);
        channel.TryWrite(NewEntry());
        await WaitForAsync(() => sink.Attempts == 1, CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(0, sink.Received);
    }

    [Fact]
    public async Task StopAsync_DrainsQueuedEntries()
    {
        var sink = new FlakySink(failures: 0) { Delay = TimeSpan.FromMilliseconds(20) };
        var (channel, service) = Build(sink, new AuditPipelineOptions());

        await service.StartAsync(CancellationToken.None);
        for (var i = 0; i < 5; i++)
        {
            channel.TryWrite(NewEntry());
        }

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(5, sink.Received);
    }

    [Fact]
    public async Task Proxy_CapturesContext_AtCallTime()
    {
        var channel = new AuditChannel();
        var provider = new MutableContextProvider { Current = new AuditContext { ActorDisplayName = "First" } };
        var proxy = AuditingDispatchProxy<ITestService>.Create(new TestService(), channel, provider, new AuditPipelineOptions { IncludeArguments = true });

        await proxy.SaveWidgetAsync("a");
        provider.Current = new AuditContext { ActorDisplayName = "Second" };

        var entry = await ReadEntryAsync(channel, CancellationToken.None);

        Assert.Equal("First", entry.ActorDisplayName);
        Assert.Equal("SaveWidget", entry.Action);
        Assert.Contains("a", entry.Arguments);
    }

    [Fact]
    public async Task Proxy_OmitsArguments_ByDefault()
    {
        var channel = new AuditChannel();
        var proxy = AuditingDispatchProxy<ITestService>.Create(new TestService(), channel, new FixedContextProvider(AuditContext.Empty), new AuditPipelineOptions());

        await proxy.SaveWidgetAsync("secret");

        var entry = await ReadEntryAsync(channel, CancellationToken.None);

        Assert.Null(entry.Arguments);
    }

    private static async Task<AuditEntry> ReadEntryAsync(AuditChannel channel, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(2));
        return await channel.Reader.ReadAsync(cts.Token);
    }

    private static (AuditChannel Channel, AuditLogBackgroundService Service) Build(IAuditSink sink, AuditPipelineOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sink);
        var provider = services.BuildServiceProvider();
        var monitor = Monitor(options);
        var channel = new AuditChannel(monitor);
        var service = new AuditLogBackgroundService(
            channel,
            new ProviderScopeFactory(provider),
            NullLogger<AuditLogBackgroundService>.Instance,
            monitor);

        return (channel, service);
    }

    private static StaticOptionsMonitor Monitor(AuditPipelineOptions options) => new(options);

    private static AuditEntry NewEntry() => new()
    {
        TimestampUtc = DateTime.UtcNow,
        ServiceName = "ITestService",
        MethodName = "SaveWidgetAsync",
        Succeeded = true,
    };

    private static System.Reflection.MethodInfo FakeMethod(string name) => new NamedMethod(name);

    private static async Task WaitForAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, cts.Token);
        }
    }

    private sealed class StaticOptionsMonitor(AuditPipelineOptions value) : IOptionsMonitor<AuditPipelineOptions>
    {
        public AuditPipelineOptions CurrentValue => value;

        public AuditPipelineOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<AuditPipelineOptions, string?> listener) => null;
    }

    private sealed class ProviderScopeFactory(IServiceProvider provider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => provider.CreateScope();
    }

    private sealed class FixedContextProvider(AuditContext context) : IAuditContextProvider
    {
        public AuditContext GetContext() => context;
    }

    private sealed class MutableContextProvider : IAuditContextProvider
    {
        public AuditContext Current { get; set; } = AuditContext.Empty;

        public AuditContext GetContext() => Current;
    }

    private sealed class FlakySink(int failures) : IAuditSink
    {
        private int _attempts;
        private int _received;

        public TimeSpan Delay { get; init; }

        public int Attempts => Volatile.Read(ref _attempts);

        public int Received => Volatile.Read(ref _received);

        public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken)
        {
            var attempt = Interlocked.Increment(ref _attempts);

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            if (attempt <= failures)
            {
                throw new InvalidOperationException("transient failure");
            }

            Interlocked.Increment(ref _received);
        }
    }

    private sealed class NamedMethod(string name) : System.Reflection.MethodInfo
    {
        public override string Name => name;

        public override System.Reflection.ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotSupportedException();

        public override System.Reflection.MethodAttributes Attributes => System.Reflection.MethodAttributes.Public;

        public override System.RuntimeMethodHandle MethodHandle => throw new NotSupportedException();

        public override Type? DeclaringType => typeof(object);

        public override Type? ReflectedType => typeof(object);

        public override System.Reflection.MethodInfo GetBaseDefinition() => this;

        public override object[] GetCustomAttributes(bool inherit) => [];

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => [];

        public override System.Reflection.MethodImplAttributes GetMethodImplementationFlags() => default;

        public override System.Reflection.ParameterInfo[] GetParameters() => [];

        public override object? Invoke(object? obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder? binder, object?[]? parameters, System.Globalization.CultureInfo? culture) => throw new NotSupportedException();

        public override bool IsDefined(Type attributeType, bool inherit) => false;
    }
}
