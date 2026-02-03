namespace Voyager.Common.Proxy.Client
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text.Json;

    /// <summary>
    /// Configuration options for the service proxy client.
    /// </summary>
    public class ServiceProxyOptions
    {
        /// <summary>
        /// Gets or sets the base URL of the service.
        /// </summary>
        /// <remarks>
        /// This is the root URL where the service is hosted.
        /// The service route prefix and method routes are appended to this URL.
        /// </remarks>
        /// <example>
        /// <code>
        /// options.BaseUrl = new Uri("https://api.example.com");
        /// </code>
        /// </example>
        public Uri? BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the timeout for HTTP requests.
        /// </summary>
        /// <remarks>
        /// Default is 30 seconds. Set to <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>
        /// for no timeout (not recommended for production).
        /// </remarks>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the JSON serializer options used for request and response serialization.
        /// </summary>
        /// <remarks>
        /// If not specified, default options with camelCase property naming are used.
        /// </remarks>
        public JsonSerializerOptions? JsonSerializerOptions { get; set; }

        /// <summary>
        /// Gets the resilience options for retry and circuit breaker policies.
        /// </summary>
        /// <remarks>
        /// Both retry and circuit breaker are disabled by default.
        /// Enable them by setting <c>Resilience.Retry.Enabled = true</c>
        /// and/or <c>Resilience.CircuitBreaker.Enabled = true</c>.
        /// </remarks>
        /// <example>
        /// <code>
        /// options.Resilience.Retry.Enabled = true;
        /// options.Resilience.Retry.MaxAttempts = 3;
        /// options.Resilience.CircuitBreaker.Enabled = true;
        /// options.Resilience.CircuitBreaker.FailureThreshold = 5;
        /// </code>
        /// </example>
        public ResilienceOptions Resilience { get; } = new ResilienceOptions();

        /// <summary>
        /// Gets or sets the delegating handler factories for the HTTP client pipeline.
        /// </summary>
        /// <remarks>
        /// These factories are used to create delegating handlers (e.g., authorization handlers)
        /// that are added to the HttpClient pipeline. This is an alternative to using
        /// <see cref="Microsoft.Extensions.DependencyInjection.HttpClientBuilderExtensions.AddHttpMessageHandler{THandler}"/>
        /// which may not work correctly with some DI container bridges (e.g., Unity).
        ///
        /// Handlers are executed in the order they are added (first added = outermost handler).
        /// </remarks>
        /// <example>
        /// <code>
        /// options.DelegatingHandlerFactories.Add(sp =>
        ///     sp.GetRequiredService&lt;AuthorizationHandler&gt;());
        /// </code>
        /// </example>
        public List<Func<IServiceProvider, DelegatingHandler>> DelegatingHandlerFactories { get; }
            = new List<Func<IServiceProvider, DelegatingHandler>>();

        /// <summary>
        /// Gets the default JSON serializer options.
        /// </summary>
        internal static JsonSerializerOptions DefaultJsonSerializerOptions { get; } = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}
