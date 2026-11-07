using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AuditLoggingPipeline;

/// <summary>
/// DI extensions for the Telemetry plugin: the pipeline itself, the <see cref="ITelemetryRecorder"/>,
/// and service registrations wrapped by <see cref="TelemetryDispatchProxy{TInterface}"/>.
/// </summary>
public static class TelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="TelemetryChannel"/>, the <see cref="TelemetryBackgroundService"/> hosted
    /// service and the <see cref="ITelemetryRecorder"/>. Register one or more <see cref="ITelemetrySink"/>
    /// implementations separately; registering none is valid and simply means drained events have
    /// nowhere to be written. Options are validated at start-up.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration of <see cref="TelemetryOptions"/>.</param>
    public static IServiceCollection AddTelemetry(
        this IServiceCollection services,
        Action<TelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<TelemetryOptions>()
            .Validate(telemetryOptions => telemetryOptions.SampleRate is > 0.0 and <= 1.0, "TelemetryOptions.SampleRate must be above 0 and at most 1.")
            .Validate(telemetryOptions => !telemetryOptions.IncludeUserKey || !string.IsNullOrWhiteSpace(telemetryOptions.UserKeySalt), "TelemetryOptions.UserKeySalt is required when IncludeUserKey is true.")
            .ValidateOnStart();

        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.AddSingleton<TelemetryChannel>();
        services.AddSingleton<ITelemetryRecorder, TelemetryRecorder>();
        services.AddHostedService<TelemetryBackgroundService>();

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TImplementation"/> as scoped and exposes it through
    /// <typeparamref name="TInterface"/> wrapped in a <see cref="TelemetryDispatchProxy{TInterface}"/>,
    /// so calls to the methods selected by <see cref="TelemetryOptions.MethodFilter"/> are timed and
    /// counted without modifying <typeparamref name="TImplementation"/>.
    /// </summary>
    public static IServiceCollection AddTelemetryScoped<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TImplementation>();

        services.AddScoped<TInterface>(provider => TelemetryDispatchProxy<TInterface>.Create(
            provider.GetRequiredService<TImplementation>(),
            provider.GetRequiredService<ITelemetryRecorder>(),
            provider.GetRequiredService<IOptionsMonitor<TelemetryOptions>>().CurrentValue));

        return services;
    }
}
