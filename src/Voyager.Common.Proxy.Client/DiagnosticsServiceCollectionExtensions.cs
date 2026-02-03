namespace Voyager.Common.Proxy.Client
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Voyager.Common.Proxy.Diagnostics;

    /// <summary>
    /// Extension methods for registering proxy diagnostics with dependency injection.
    /// </summary>
    public static class DiagnosticsServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a proxy diagnostics handler to the service collection.
        /// </summary>
        /// <typeparam name="THandler">The diagnostics handler type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <example>
        /// <code>
        /// services.AddProxyDiagnostics&lt;MyCustomDiagnosticsHandler&gt;();
        /// </code>
        /// </example>
        public static IServiceCollection AddProxyDiagnostics<THandler>(this IServiceCollection services)
            where THandler : class, IProxyDiagnostics
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddTransient<IProxyDiagnostics, THandler>();
            return services;
        }

        /// <summary>
        /// Adds a proxy diagnostics handler instance to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="handler">The diagnostics handler instance.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <example>
        /// <code>
        /// services.AddProxyDiagnostics(new MyCustomDiagnosticsHandler());
        /// </code>
        /// </example>
        public static IServiceCollection AddProxyDiagnostics(
            this IServiceCollection services,
            IProxyDiagnostics handler)
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            services.AddSingleton(handler);
            return services;
        }

        /// <summary>
        /// Adds a proxy request context provider to the service collection.
        /// </summary>
        /// <typeparam name="TContext">The request context type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// The request context provides user information (login, unit ID, etc.)
        /// that is attached to all diagnostic events.
        /// Only one context provider should be registered.
        /// </remarks>
        /// <example>
        /// <code>
        /// services.AddProxyRequestContext&lt;HttpContextRequestContext&gt;();
        /// </code>
        /// </example>
        public static IServiceCollection AddProxyRequestContext<TContext>(this IServiceCollection services)
            where TContext : class, IProxyRequestContext
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddScoped<IProxyRequestContext, TContext>();
            return services;
        }
    }
}
