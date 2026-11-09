namespace AuditLoggingPipeline.Sql.Tests;

using AuditLoggingPipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public class SqlAuditRegistrationTests
{
    private static ServiceProvider BuildAudit(Action<SqlAuditSinkOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddSqlAuditSink(configure);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildTrail(Action<SqlStateTrailSinkOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddSqlStateTrailSink(configure);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddSqlAuditSink_RegistersSqlSinkAsAuditSink()
    {
        using var provider = BuildAudit(options => options.ConnectionString = "Server=x");

        Assert.IsType<SqlAuditSink>(Assert.Single(provider.GetServices<IAuditSink>()));
    }

    [Fact]
    public void AddSqlStateTrailSink_RegistersSqlSinkAsTrackerSink()
    {
        using var provider = BuildTrail(options =>
        {
            options.ConnectionString = "Server=x";
            options.Targets["Users"] = "dbo.UsersAudit_Insert";
        });

        Assert.IsType<SqlStateTrailSink>(Assert.Single(provider.GetServices<ITrackerSink>()));
    }

    [Fact]
    public void AddSqlAuditSink_CalledTwice_RegistersOneSink()
    {
        var services = new ServiceCollection();
        services.AddSqlAuditSink(options => options.ConnectionString = "Server=x");
        services.AddSqlAuditSink(options => options.ConnectionString = "Server=y");
        using var provider = services.BuildServiceProvider();

        Assert.Single(provider.GetServices<IAuditSink>());
    }

    [Fact]
    public void AddSqlAuditSink_AppliesDefaults()
    {
        using var provider = BuildAudit(options => options.ConnectionString = "Server=x");

        var options = provider.GetRequiredService<IOptions<SqlAuditSinkOptions>>().Value;

        Assert.Equal("dbo.SecurityAudit_Insert", options.ProcedureName);
        Assert.Equal("@Entries", options.ParameterName);
        Assert.Equal("dbo.AuditList", options.TableTypeName);
        Assert.Equal(30, options.CommandTimeoutSeconds);
    }

    [Fact]
    public void AddSqlStateTrailSink_TargetsAreCaseInsensitive()
    {
        using var provider = BuildTrail(options =>
        {
            options.ConnectionString = "Server=x";
            options.Targets["Users"] = "dbo.UsersAudit_Insert";
        });

        var options = provider.GetRequiredService<IOptions<SqlStateTrailSinkOptions>>().Value;

        Assert.Equal("dbo.UsersAudit_Insert", options.Targets["USERS"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AuditOptions_WithoutConnectionString_FailValidation(string connectionString)
    {
        using var provider = BuildAudit(options => options.ConnectionString = connectionString);

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqlAuditSinkOptions>>().Value);

        Assert.Contains("ConnectionString", exception.Message);
    }

    [Fact]
    public void AuditOptions_WithBlankProcedureName_FailValidation()
    {
        using var provider = BuildAudit(options =>
        {
            options.ConnectionString = "Server=x";
            options.ProcedureName = " ";
        });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqlAuditSinkOptions>>().Value);

        Assert.Contains("ProcedureName", exception.Message);
    }

    [Fact]
    public void AuditOptions_WithNonPositiveTimeout_FailValidation()
    {
        using var provider = BuildAudit(options =>
        {
            options.ConnectionString = "Server=x";
            options.CommandTimeoutSeconds = 0;
        });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqlAuditSinkOptions>>().Value);

        Assert.Contains("CommandTimeoutSeconds", exception.Message);
    }

    [Fact]
    public void TrailOptions_WithoutConnectionString_FailValidation()
    {
        using var provider = BuildTrail(options => options.Targets["Users"] = "dbo.UsersAudit_Insert");

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqlStateTrailSinkOptions>>().Value);

        Assert.Contains("ConnectionString", exception.Message);
    }

    [Fact]
    public void TrailOptions_WithoutTargets_FailValidation()
    {
        using var provider = BuildTrail(options => options.ConnectionString = "Server=x");

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqlStateTrailSinkOptions>>().Value);

        Assert.Contains("Targets", exception.Message);
    }

    [Fact]
    public void TrailOptions_WithBlankTargetProcedure_FailValidation()
    {
        using var provider = BuildTrail(options =>
        {
            options.ConnectionString = "Server=x";
            options.Targets["Users"] = " ";
        });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<SqlStateTrailSinkOptions>>().Value);

        Assert.Contains("Targets", exception.Message);
    }
}
