namespace AuditLoggingPipeline;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// DI extensions for registering the audit-logging infrastructure and for registering
/// service implementations wrapped by <see cref="AuditingDispatchProxy{TInterface}"/> so that
/// mutating calls (Save/Add/Update/Archive/Delete) are captured without any change to the
/// service classes themselves.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared <see cref="AuditChannel"/> and the <see cref="AuditLogBackgroundService"/>
    /// hosted service. Call once during startup. Register one or more <see cref="IAuditSink"/>
    /// implementations separately (via <c>services.AddScoped&lt;IAuditSink, TSink&gt;()</c> or
    /// similar) for whichever purposes (action log, state trail, etc.) the consuming app needs;
    /// registering none is valid and simply means drained entries have nowhere to be written.
    /// </summary>
    public static IServiceCollection AddAuditLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddSingleton<AuditChannel>();
        services.AddHostedService<AuditLogBackgroundService>();

        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TImplementation"/> as scoped and exposes it through
    /// <typeparamref name="TInterface"/> wrapped in an <see cref="AuditingDispatchProxy{TInterface}"/>,
    /// so mutating calls made through <typeparamref name="TInterface"/> are audited without
    /// modifying <typeparamref name="TImplementation"/>.
    /// </summary>
    public static IServiceCollection AddAuditedScoped<TInterface, TImplementation>(this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<TImplementation>();

        services.AddScoped<TInterface>(provider =>
        {
            var decorated = provider.GetRequiredService<TImplementation>();
            var channel = provider.GetRequiredService<AuditChannel>();
            var httpContextAccessor = provider.GetService<IHttpContextAccessor>();
            var logger = provider.GetService<ILogger<AuditingDispatchProxy<TInterface>>>();

            return AuditingDispatchProxy<TInterface>.Create(decorated, channel, httpContextAccessor, logger);
        });

        return services;
    }
}
