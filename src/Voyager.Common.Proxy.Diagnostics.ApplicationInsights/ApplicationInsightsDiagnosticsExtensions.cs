namespace Voyager.Common.Proxy.Diagnostics.ApplicationInsights
{
	using System;
	using Microsoft.ApplicationInsights;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>
	/// Extension methods for registering Application Insights diagnostics.
	/// </summary>
	public static class ApplicationInsightsDiagnosticsExtensions
	{
		/// <summary>
		/// Adds the Application Insights diagnostics handler to the service collection.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <returns>The service collection for chaining.</returns>
		/// <remarks>
		/// Requires <see cref="TelemetryClient"/> to be registered in the service collection
		/// (typically via <c>services.AddApplicationInsightsTelemetry()</c>).
		/// </remarks>
		/// <example>
		/// <code>
		/// services.AddApplicationInsightsTelemetry();
		/// services.AddProxyApplicationInsightsDiagnostics();
		/// services.AddServiceProxy&lt;IUserService&gt;("https://api.example.com");
		/// </code>
		/// </example>
		public static IServiceCollection AddProxyApplicationInsightsDiagnostics(this IServiceCollection services)
		{
			if (services is null)
			{
				throw new ArgumentNullException(nameof(services));
			}

			services.AddSingleton<IProxyDiagnostics>(sp =>
			{
				var client = sp.GetRequiredService<TelemetryClient>();
				return new ApplicationInsightsProxyDiagnostics(client);
			});

			return services;
		}

		/// <summary>
		/// Adds the Application Insights diagnostics handler with configuration.
		/// </summary>
		/// <param name="services">The service collection.</param>
		/// <param name="configure">Action to configure <see cref="ApplicationInsightsOptions"/>.</param>
		/// <returns>The service collection for chaining.</returns>
		/// <example>
		/// <code>
		/// services.AddApplicationInsightsTelemetry();
		/// services.AddProxyApplicationInsightsDiagnostics(options =>
		/// {
		///     options.CloudRoleName = "MyService-Production";
		/// });
		/// </code>
		/// </example>
		public static IServiceCollection AddProxyApplicationInsightsDiagnostics(
			this IServiceCollection services,
			Action<ApplicationInsightsOptions> configure)
		{
			if (services is null)
			{
				throw new ArgumentNullException(nameof(services));
			}

			if (configure is null)
			{
				throw new ArgumentNullException(nameof(configure));
			}

			var options = new ApplicationInsightsOptions();
			configure(options);

			services.AddSingleton<IProxyDiagnostics>(sp =>
			{
				var client = sp.GetRequiredService<TelemetryClient>();
				return new ApplicationInsightsProxyDiagnostics(client, options);
			});

			return services;
		}
	}
}
