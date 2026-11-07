namespace AuditLoggingPipeline;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// DI extensions for the Audit plugin: the pipeline itself, the <see cref="IAuditRecorder"/>, and
/// service registrations wrapped by <see cref="AuditingDispatchProxy{TInterface}"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the <see cref="AuditChannel"/>, the <see cref="AuditLogBackgroundService"/> hosted
	/// service, the <see cref="IAuditRecorder"/> and a default HTTP-based <see cref="IAuditContextProvider"/>
	/// (replace it by registering your own before this call). Register one or more
	/// <see cref="IAuditSink"/> implementations separately; registering none is valid and simply
	/// means drained entries have nowhere to be written.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration of <see cref="AuditPipelineOptions"/>.</param>
	public static IServiceCollection AddAuditLogging(
		this IServiceCollection services,
		Action<AuditPipelineOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var options = services.AddOptions<AuditPipelineOptions>();
		if (configure is not null)
		{
			options.Configure(configure);
		}

		services.AddHttpContextAccessor();
		services.TryAddSingleton<IAuditContextProvider, HttpContextAuditContextProvider>();
		services.AddSingleton<AuditChannel>();
		services.AddSingleton<IAuditRecorder, AuditRecorder>();
		services.AddHostedService<AuditLogBackgroundService>();

		return services;
	}

	/// <summary>
	/// Registers <typeparamref name="TImplementation"/> as scoped and exposes it through
	/// <typeparamref name="TInterface"/> wrapped in an <see cref="AuditingDispatchProxy{TInterface}"/>,
	/// so calls to the methods selected by <see cref="AuditPipelineOptions"/> are audited without
	/// modifying <typeparamref name="TImplementation"/>.
	/// </summary>
	public static IServiceCollection AddAuditedScoped<TInterface, TImplementation>(this IServiceCollection services)
		where TInterface : class
		where TImplementation : class, TInterface
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddScoped<TImplementation>();

		services.AddScoped<TInterface>(provider => AuditingDispatchProxy<TInterface>.Create(
			provider.GetRequiredService<TImplementation>(),
			provider.GetRequiredService<AuditChannel>(),
			provider.GetRequiredService<IAuditContextProvider>(),
			provider.GetRequiredService<IOptionsMonitor<AuditPipelineOptions>>().CurrentValue,
			provider.GetService<ILogger<AuditingDispatchProxy<TInterface>>>()));

		return services;
	}
}
