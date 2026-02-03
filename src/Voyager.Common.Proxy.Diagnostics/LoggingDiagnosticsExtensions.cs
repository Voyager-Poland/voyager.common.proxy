namespace Voyager.Common.Proxy.Diagnostics
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for registering logging diagnostics.
    /// </summary>
    public static class LoggingDiagnosticsExtensions
    {
        /// <summary>
        /// Adds the logging diagnostics handler to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// Requires <see cref="Microsoft.Extensions.Logging.ILogger{T}"/> to be registered.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddLogging();
        /// services.AddProxyLoggingDiagnostics();
        /// services.AddServiceProxy&lt;IUserService&gt;("https://api.example.com");
        /// </code>
        /// </example>
        public static IServiceCollection AddProxyLoggingDiagnostics(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddTransient<IProxyDiagnostics, LoggingProxyDiagnostics>();
            return services;
        }
    }
}
