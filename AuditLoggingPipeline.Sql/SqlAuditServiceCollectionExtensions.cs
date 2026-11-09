using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuditLoggingPipeline;

/// <summary>DI extensions for the SQL audit and state-trail sinks.</summary>
public static class SqlAuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SqlAuditSink"/> as an <see cref="IAuditSink"/>. It runs alongside any other
    /// registered sinks. Options are validated at start-up, so a missing connection string stops the host
    /// instead of silently dropping entries.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures <see cref="SqlAuditSinkOptions"/>; at least the connection string is required.</param>
    public static IServiceCollection AddSqlAuditSink(
        this IServiceCollection services,
        Action<SqlAuditSinkOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<SqlAuditSinkOptions>()
            .Configure(configure)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "SqlAuditSinkOptions.ConnectionString is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ProcedureName), "SqlAuditSinkOptions.ProcedureName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ParameterName), "SqlAuditSinkOptions.ParameterName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.TableTypeName), "SqlAuditSinkOptions.TableTypeName is required.")
            .Validate(options => options.CommandTimeoutSeconds > 0, "SqlAuditSinkOptions.CommandTimeoutSeconds must be above 0.")
            .ValidateOnStart();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuditSink, SqlAuditSink>());

        return services;
    }

    /// <summary>
    /// Registers <see cref="SqlStateTrailSink"/> as an <see cref="ITrackerSink"/>. It runs alongside any other
    /// registered sinks. Options are validated at start-up, so a missing connection string or an empty target
    /// map stops the host instead of silently dropping entries.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures <see cref="SqlStateTrailSinkOptions"/>; the connection string and at least one target are required.</param>
    public static IServiceCollection AddSqlStateTrailSink(
        this IServiceCollection services,
        Action<SqlStateTrailSinkOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<SqlStateTrailSinkOptions>()
            .Configure(configure)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "SqlStateTrailSinkOptions.ConnectionString is required.")
            .Validate(options => options.Targets.Count > 0, "SqlStateTrailSinkOptions.Targets needs at least one entry.")
            .Validate(options => options.Targets.Values.All(procedure => !string.IsNullOrWhiteSpace(procedure)), "SqlStateTrailSinkOptions.Targets cannot have a blank procedure name.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ParameterName), "SqlStateTrailSinkOptions.ParameterName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.TableTypeName), "SqlStateTrailSinkOptions.TableTypeName is required.")
            .Validate(options => options.CommandTimeoutSeconds > 0, "SqlStateTrailSinkOptions.CommandTimeoutSeconds must be above 0.")
            .ValidateOnStart();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITrackerSink, SqlStateTrailSink>());

        return services;
    }
}
