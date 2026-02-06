namespace Voyager.Common.Proxy.Diagnostics
{
    using System;
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for registering console diagnostics.
    /// </summary>
    public static class ConsoleDiagnosticsExtensions
    {
        /// <summary>
        /// Adds the console diagnostics handler to the service collection.
        /// Writes proxy events directly to <see cref="System.Console"/> using string interpolation,
        /// without depending on any logging framework.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <example>
        /// <code>
        /// services.AddProxyConsoleDiagnostics();
        /// services.AddServiceProxy&lt;IUserService&gt;("https://api.example.com");
        /// </code>
        /// </example>
        public static IServiceCollection AddProxyConsoleDiagnostics(this IServiceCollection services)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddTransient<IProxyDiagnostics, ConsoleProxyDiagnostics>();
            return services;
        }
    }
}
