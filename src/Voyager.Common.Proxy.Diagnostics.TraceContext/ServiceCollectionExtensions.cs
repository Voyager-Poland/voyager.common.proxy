#if NET6_0_OR_GREATER
namespace Voyager.Common.Proxy.Diagnostics.TraceContext
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Voyager.Common.Proxy.Diagnostics;

    /// <summary>
    /// Extension methods for configuring TraceContext integration with proxy diagnostics.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds TraceContext-aware proxy request context to the service collection.
        /// This enables automatic trace context propagation in diagnostic events.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// Requires <see cref="ITraceContextAccessor"/> to be registered.
        /// Use <c>services.AddTraceContext()</c> from Voyager.TraceContext.Core first.
        /// </remarks>
        public static IServiceCollection AddProxyTraceContext(this IServiceCollection services)
        {
            services.TryAddScoped<IProxyRequestContext>(sp =>
            {
                var accessor = sp.GetRequiredService<ITraceContextAccessor>();
                return new TraceContextProxyRequestContext(accessor);
            });

            return services;
        }

        /// <summary>
        /// Adds TraceContext-aware proxy request context that wraps an existing context.
        /// This enables automatic trace context propagation while preserving user context.
        /// </summary>
        /// <typeparam name="TInner">The inner request context type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// Requires <see cref="ITraceContextAccessor"/> to be registered.
        /// Use <c>services.AddTraceContext()</c> from Voyager.TraceContext.Core first.
        /// </remarks>
        public static IServiceCollection AddProxyTraceContext<TInner>(this IServiceCollection services)
            where TInner : class, IProxyRequestContext
        {
            // Register inner context
            services.TryAddScoped<TInner>();

            // Replace IProxyRequestContext with wrapper
            services.AddScoped<IProxyRequestContext>(sp =>
            {
                var inner = sp.GetRequiredService<TInner>();
                var accessor = sp.GetRequiredService<ITraceContextAccessor>();
                return new TraceContextProxyRequestContext(inner, accessor);
            });

            return services;
        }
    }
}
#endif
