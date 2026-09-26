namespace AuditLoggingPipeline;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// DI extensions for registering the state-trail tracking infrastructure and for registering
/// service implementations wrapped by <see cref="TrackingDispatchProxy{TInterface}"/> so that
/// mutating calls (Save/Add/Update/Archive/Delete) are captured without any change to the
/// service classes themselves. Fully independent of the Audit plugin's
/// <c>ServiceCollectionExtensions</c>; a consumer may install this package with or without
/// AuditLoggingPipeline installed.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared <see cref="TrackerChannel"/> and the
    /// <see cref="TrackerLogBackgroundService"/> hosted service. Call once during startup.
    /// Register one or more <see cref="ITrackerSink"/> implementations separately (via
    /// <c>services.AddScoped&lt;ITrackerSink, TSink&gt;()</c> or similar); registering none is
    /// valid and simply means drained entries have nowhere to be written.
    /// </summary>
    public static IServiceCollection AddStateTrailTracking(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddSingleton<TrackerChannel>();
        services.AddHostedService<TrackerLogBackgroundService>();

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TImplementation"/> as scoped and exposes it through
    /// <typeparamref name="TInterface"/> wrapped in a
    /// <see cref="TrackingDispatchProxy{TInterface}"/>, so mutating calls made through
    /// <typeparamref name="TInterface"/> have their before/after entity state tracked without
    /// modifying <typeparamref name="TImplementation"/>.
    /// </summary>
    public static IServiceCollection AddTrackedScoped<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TImplementation>();

        services.AddScoped<TInterface>(provider =>
        {
            var decorated = provider.GetRequiredService<TImplementation>();
            var channel = provider.GetRequiredService<TrackerChannel>();
            var httpContextAccessor = provider.GetService<IHttpContextAccessor>();
            var logger = provider.GetService<ILogger<TrackingDispatchProxy<TInterface>>>();

            return TrackingDispatchProxy<TInterface>.Create(decorated, channel, httpContextAccessor, logger);
        });

        return services;
    }
}
