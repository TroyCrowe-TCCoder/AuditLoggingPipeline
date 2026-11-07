namespace AuditLoggingPipeline;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// DI extensions for the Tracker plugin: the pipeline itself, the <see cref="IStateTrailRecorder"/>,
/// and service registrations wrapped by <see cref="TrackingDispatchProxy{TInterface}"/>. Independent
/// of the Audit plugin; a consumer may install this package with or without it.
/// </summary>
public static class ServiceCollectionExtensions
{
	/// <summary>
	/// Registers the <see cref="TrackerChannel"/>, the <see cref="TrackerLogBackgroundService"/> hosted
	/// service, the <see cref="IStateTrailRecorder"/> and a default HTTP-based <see cref="IAuditContextProvider"/>
	/// (replace it by registering your own before this call). Register one or more
	/// <see cref="ITrackerSink"/> implementations separately; registering none is valid and simply
	/// means drained entries have nowhere to be written.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration of <see cref="StateTrailOptions"/>.</param>
	public static IServiceCollection AddStateTrailTracking(
		this IServiceCollection services,
		Action<StateTrailOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var options = services.AddOptions<StateTrailOptions>();
		if (configure is not null)
		{
			options.Configure(configure);
		}

		services.AddHttpContextAccessor();
		services.TryAddSingleton<IAuditContextProvider, HttpContextAuditContextProvider>();
		services.AddSingleton<TrackerChannel>();
		services.AddSingleton<IStateTrailRecorder, StateTrailRecorder>();
		services.AddHostedService<TrackerLogBackgroundService>();

		return services;
	}

	/// <summary>
	/// Registers <typeparamref name="TImplementation"/> as scoped and exposes it through
	/// <typeparamref name="TInterface"/> wrapped in a <see cref="TrackingDispatchProxy{TInterface}"/>,
	/// so calls to the methods selected by <see cref="StateTrailOptions"/> have their entity state
	/// tracked without modifying <typeparamref name="TImplementation"/>.
	/// </summary>
	public static IServiceCollection AddTrackedScoped<TInterface, TImplementation>(this IServiceCollection services)
		where TInterface : class
		where TImplementation : class, TInterface
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddScoped<TImplementation>();

		services.AddScoped<TInterface>(provider => TrackingDispatchProxy<TInterface>.Create(
			provider.GetRequiredService<TImplementation>(),
			provider.GetRequiredService<TrackerChannel>(),
			provider.GetRequiredService<IAuditContextProvider>(),
			provider.GetRequiredService<IOptionsMonitor<StateTrailOptions>>().CurrentValue,
			provider.GetService<ILogger<TrackingDispatchProxy<TInterface>>>()));

		return services;
	}
}
