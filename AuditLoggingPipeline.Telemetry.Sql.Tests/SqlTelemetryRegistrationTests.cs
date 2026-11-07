namespace AuditLoggingPipeline.Telemetry.Sql.Tests;

using AuditLoggingPipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public class SqlTelemetryRegistrationTests
{
    private static ServiceProvider Build(Action<SqlTelemetrySinkOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddSqlTelemetrySink(configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddSqlTelemetrySink_RegistersSqlSinkAsTelemetrySink()
    {
        using var provider = Build(options => options.ConnectionString = "Server=x");

        var sink = Assert.Single(provider.GetServices<ITelemetrySink>());

        Assert.IsType<SqlTelemetrySink>(sink);
    }

    [Fact]
    public void AddSqlTelemetrySink_CalledTwice_RegistersOneSink()
    {
        var services = new ServiceCollection();
        services.AddSqlTelemetrySink(options => options.ConnectionString = "Server=x");
        services.AddSqlTelemetrySink(options => options.ConnectionString = "Server=y");
        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<ITelemetrySink>());
    }

    [Fact]
    public void AddSqlTelemetrySink_AppliesDefaults()
    {
        using var provider = Build(options => options.ConnectionString = "Server=x");

        var options = provider.GetRequiredService<IOptions<SqlTelemetrySinkOptions>>().Value;

        Assert.Equal("dbo.UsageEvent_Insert", options.ProcedureName);
        Assert.Equal("@Events", options.ParameterName);
        Assert.Equal("dbo.UsageEventList", options.TableTypeName);
        Assert.Equal(100, options.ChunkSize);
        Assert.Equal(30, options.CommandTimeoutSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Options_WithoutConnectionString_FailValidation(string connectionString)
    {
        using var provider = Build(options => options.ConnectionString = connectionString);

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqlTelemetrySinkOptions>>().Value);

        Assert.Contains("ConnectionString", exception.Message);
    }

    [Fact]
    public void Options_WithNonPositiveChunkSize_FailValidation()
    {
        using var provider = Build(options =>
        {
            options.ConnectionString = "Server=x";
            options.ChunkSize = 0;
        });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqlTelemetrySinkOptions>>().Value);

        Assert.Contains("ChunkSize", exception.Message);
    }

    [Fact]
    public void Options_WithBlankProcedureName_FailValidation()
    {
        using var provider = Build(options =>
        {
            options.ConnectionString = "Server=x";
            options.ProcedureName = " ";
        });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqlTelemetrySinkOptions>>().Value);

        Assert.Contains("ProcedureName", exception.Message);
    }
}
