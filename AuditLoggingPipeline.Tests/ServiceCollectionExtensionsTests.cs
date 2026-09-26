namespace AuditLoggingPipeline.Tests;

using Microsoft.Extensions.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAuditLogging_RegistersChannelAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddAuditLogging();
        var provider = services.BuildServiceProvider();

        var channel1 = provider.GetRequiredService<AuditChannel>();
        var channel2 = provider.GetRequiredService<AuditChannel>();

        Assert.Same(channel1, channel2);
    }

    [Fact]
    public void AddAuditLogging_ThrowsArgumentNullException_WhenServicesIsNull()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddAuditLogging());
    }

    [Fact]
    public void AddAuditedScoped_ResolvesProxyImplementingInterface()
    {
        var services = new ServiceCollection();
        services.AddAuditLogging();
        services.AddAuditedScoped<ITestService, TestService>();

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<ITestService>();

        Assert.IsAssignableFrom<ITestService>(resolved);
        Assert.False(resolved is TestService);
    }

    [Fact]
    public void AddAuditedScoped_ThrowsArgumentNullException_WhenServicesIsNull()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddAuditedScoped<ITestService, TestService>());
    }
}
