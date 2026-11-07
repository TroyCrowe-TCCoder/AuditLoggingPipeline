using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuditLoggingPipeline;

/// <summary>DI extensions for the SQL telemetry sink.</summary>
public static class SqlTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SqlTelemetrySink"/> as an <see cref="ITelemetrySink"/>. It runs alongside any
    /// other registered sinks. Options are validated at start-up, so a missing connection string stops the
    /// host instead of silently dropping events.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures <see cref="SqlTelemetrySinkOptions"/>; at least the connection string is required.</param>
    public static IServiceCollection AddSqlTelemetrySink(
        this IServiceCollection services,
        Action<SqlTelemetrySinkOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<SqlTelemetrySinkOptions>()
            .Configure(configure)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "SqlTelemetrySinkOptions.ConnectionString is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ProcedureName), "SqlTelemetrySinkOptions.ProcedureName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ParameterName), "SqlTelemetrySinkOptions.ParameterName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.TableTypeName), "SqlTelemetrySinkOptions.TableTypeName is required.")
            .Validate(options => options.ChunkSize > 0, "SqlTelemetrySinkOptions.ChunkSize must be above 0.")
            .Validate(options => options.CommandTimeoutSeconds > 0, "SqlTelemetrySinkOptions.CommandTimeoutSeconds must be above 0.")
            .ValidateOnStart();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<ITelemetrySink, SqlTelemetrySink>());

        return services;
    }
}
