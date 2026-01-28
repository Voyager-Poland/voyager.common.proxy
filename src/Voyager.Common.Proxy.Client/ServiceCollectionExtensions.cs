namespace Voyager.Common.Proxy.Client
{
    using System;
    using System.Net.Http;
    using Microsoft.Extensions.DependencyInjection;

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
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is null.
        /// </exception>
        /// <example>
        /// <code>
        /// services.AddServiceProxy&lt;IUserService&gt;(options =>
        /// {
        ///     options.BaseUrl = new Uri("https://api.example.com");
        ///     options.Timeout = TimeSpan.FromSeconds(30);
        /// });
        /// </code>
        /// </example>
        public static IServiceCollection AddServiceProxy<TService>(
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
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="baseUrl"/> is null.
        /// </exception>
        /// <example>
        /// <code>
        /// services.AddServiceProxy&lt;IUserService&gt;("https://api.example.com");
        /// </code>
        /// </example>
        public static IServiceCollection AddServiceProxy<TService>(
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
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="baseUrl"/> is null.
        /// </exception>
        public static IServiceCollection AddServiceProxy<TService>(
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

        private static IServiceCollection AddServiceProxyCore<TService>(
            IServiceCollection services,
            ServiceProxyOptions options)
            where TService : class
        {
            if (options.BaseUrl is null)
            {
                throw new InvalidOperationException(
                    $"BaseUrl must be configured for service proxy {typeof(TService).Name}.");
            }

            var clientName = $"ServiceProxy_{typeof(TService).FullName}";

            // Register HttpClient with HttpClientFactory
            services.AddHttpClient(clientName, client =>
            {
                client.BaseAddress = options.BaseUrl;
                client.Timeout = options.Timeout;
            });

            // Register the service proxy
            services.AddTransient<TService>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(clientName);

                return ServiceProxy<TService>.Create(httpClient, options);
            });

            return services;
        }
    }
}
