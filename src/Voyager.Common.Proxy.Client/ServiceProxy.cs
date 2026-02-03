namespace Voyager.Common.Proxy.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Voyager.Common.Proxy.Client.Abstractions;
    using Voyager.Common.Proxy.Client.Internal;
    using Voyager.Common.Proxy.Diagnostics;
    using Voyager.Common.Resilience;

    /// <summary>
    /// Factory for creating HTTP service proxies.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <remarks>
    /// This class acts as a facade, delegating proxy creation to the appropriate
    /// <see cref="IProxyFactory"/> implementation based on the runtime platform.
    /// </remarks>
    public static class ServiceProxy<TService>
        where TService : class
    {
        /// <summary>
        /// Creates a new instance of the service proxy.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="options">The proxy configuration options.</param>
        /// <returns>A proxy instance that implements <typeparamref name="TService"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is null.
        /// </exception>
        public static TService Create(HttpClient httpClient, ServiceProxyOptions options)
        {
            return Create(httpClient, options, null, null, null);
        }

        /// <summary>
        /// Creates a new instance of the service proxy with optional circuit breaker and diagnostics.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="options">The proxy configuration options.</param>
        /// <param name="circuitBreaker">Optional shared circuit breaker policy instance.</param>
        /// <param name="diagnosticsHandlers">Optional collection of diagnostics handlers.</param>
        /// <param name="requestContext">Optional request context provider for user information.</param>
        /// <returns>A proxy instance that implements <typeparamref name="TService"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is null.
        /// </exception>
        internal static TService Create(
            HttpClient httpClient,
            ServiceProxyOptions options,
            CircuitBreakerPolicy? circuitBreaker,
            IEnumerable<IProxyDiagnostics>? diagnosticsHandlers,
            IProxyRequestContext? requestContext)
        {
            if (httpClient is null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var interceptor = new HttpMethodInterceptor(
                httpClient,
                options,
                typeof(TService),
                circuitBreaker,
                diagnosticsHandlers,
                requestContext);

            var factory = ProxyFactoryProvider.GetFactory();

            return factory.CreateProxy<TService>(interceptor);
        }
    }
}
