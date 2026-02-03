namespace Voyager.Common.Proxy.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Voyager.Common.Proxy.Diagnostics;
    using Voyager.Common.Resilience;

    /// <summary>
    /// Extension methods for registering service proxies with dependency injection.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds a service proxy client to the service collection.
        /// </summary>
        /// <typeparam name="TService">The service interface type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Action to configure the proxy options.</param>
        /// <returns>
        /// An <see cref="IHttpClientBuilder"/> that can be used to configure the underlying HTTP client,
        /// including adding message handlers for authentication, retry policies, etc.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is null.
        /// </exception>
        /// <example>
        /// <code>
        /// // Basic usage
        /// services.AddServiceProxy&lt;IUserService&gt;(options =>
        /// {
        ///     options.BaseUrl = new Uri("https://api.example.com");
        ///     options.Timeout = TimeSpan.FromSeconds(30);
        /// });
        ///
        /// // With authentication handler
        /// services.AddServiceProxy&lt;IUserService&gt;(options =>
        /// {
        ///     options.BaseUrl = new Uri("https://api.example.com");
        /// })
        /// .AddHttpMessageHandler&lt;AuthorizationHandler&gt;();
        ///
        /// // With Polly retry policy
        /// services.AddServiceProxy&lt;IUserService&gt;(options =>
        /// {
        ///     options.BaseUrl = new Uri("https://api.example.com");
        /// })
        /// .AddHttpMessageHandler&lt;AuthorizationHandler&gt;()
        /// .AddPolicyHandler(GetRetryPolicy());
        /// </code>
        /// </example>
        public static IHttpClientBuilder AddServiceProxy<TService>(
            this IServiceCollection services,
            Action<ServiceProxyOptions> configureOptions)
            where TService : class
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (configureOptions is null)
            {
                throw new ArgumentNullException(nameof(configureOptions));
            }

            var options = new ServiceProxyOptions();
            configureOptions(options);

            return AddServiceProxyCore<TService>(services, options);
        }

        /// <summary>
        /// Adds a service proxy client to the service collection with a base URL.
        /// </summary>
        /// <typeparam name="TService">The service interface type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="baseUrl">The base URL of the service.</param>
        /// <returns>
        /// An <see cref="IHttpClientBuilder"/> that can be used to configure the underlying HTTP client,
        /// including adding message handlers for authentication, retry policies, etc.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="baseUrl"/> is null.
        /// </exception>
        /// <example>
        /// <code>
        /// // Simple usage
        /// services.AddServiceProxy&lt;IUserService&gt;("https://api.example.com");
        ///
        /// // With authentication
        /// services.AddServiceProxy&lt;IUserService&gt;("https://api.example.com")
        ///     .AddHttpMessageHandler&lt;AuthorizationHandler&gt;();
        /// </code>
        /// </example>
        public static IHttpClientBuilder AddServiceProxy<TService>(
            this IServiceCollection services,
            string baseUrl)
            where TService : class
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                throw new ArgumentNullException(nameof(baseUrl));
            }

            var options = new ServiceProxyOptions
            {
                BaseUrl = new Uri(baseUrl)
            };

            return AddServiceProxyCore<TService>(services, options);
        }

        /// <summary>
        /// Adds a service proxy client to the service collection with a base URI.
        /// </summary>
        /// <typeparam name="TService">The service interface type.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="baseUrl">The base URI of the service.</param>
        /// <returns>
        /// An <see cref="IHttpClientBuilder"/> that can be used to configure the underlying HTTP client,
        /// including adding message handlers for authentication, retry policies, etc.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="baseUrl"/> is null.
        /// </exception>
        public static IHttpClientBuilder AddServiceProxy<TService>(
            this IServiceCollection services,
            Uri baseUrl)
            where TService : class
        {
            if (services is null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            if (baseUrl is null)
            {
                throw new ArgumentNullException(nameof(baseUrl));
            }

            var options = new ServiceProxyOptions
            {
                BaseUrl = baseUrl
            };

            return AddServiceProxyCore<TService>(services, options);
        }

        private static IHttpClientBuilder AddServiceProxyCore<TService>(
            IServiceCollection services,
            ServiceProxyOptions options)
            where TService : class
        {
            if (options.BaseUrl is null)
            {
                throw new InvalidOperationException(
                    $"BaseUrl must be configured for service proxy {typeof(TService).Name}.");
            }

            var clientName = GetHttpClientName<TService>();

            // Register HttpClient with HttpClientFactory
            var httpClientBuilder = services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = options.BaseUrl;
                client.Timeout = options.Timeout;
            });

            // Create shared circuit breaker if enabled (one per service type)
            CircuitBreakerPolicy? circuitBreaker = null;
            if (options.Resilience.CircuitBreaker.Enabled)
            {
                var cbOptions = options.Resilience.CircuitBreaker;
                circuitBreaker = new CircuitBreakerPolicy(
                    failureThreshold: cbOptions.FailureThreshold,
                    openTimeout: cbOptions.OpenTimeout,
                    halfOpenMaxAttempts: cbOptions.HalfOpenSuccessThreshold);
            }

            // Register the service proxy
            services.AddTransient<TService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(clientName);

                // Defensive fallback: ensure BaseAddress is set
                // This handles cases where IHttpClientFactory integration doesn't work properly
                // (e.g., some Unity/DI container bridging scenarios)
                if (httpClient.BaseAddress == null && options.BaseUrl != null)
                {
                    httpClient.BaseAddress = options.BaseUrl;
                }

                // Resolve diagnostics handlers (optional)
                var diagnosticsHandlers = sp.GetServices<IProxyDiagnostics>();

                // Resolve request context (optional)
                var requestContext = sp.GetService<IProxyRequestContext>();

                return ServiceProxy<TService>.Create(
                    httpClient,
                    options,
                    circuitBreaker,
                    diagnosticsHandlers,
                    requestContext);
            });

            return httpClientBuilder;
        }

        /// <summary>
        /// Gets the HTTP client name for a service type.
        /// </summary>
        /// <typeparam name="TService">The service interface type.</typeparam>
        /// <returns>The HTTP client name.</returns>
        internal static string GetHttpClientName<TService>()
        {
            return $"ServiceProxy_{typeof(TService).FullName}";
        }
    }
}
