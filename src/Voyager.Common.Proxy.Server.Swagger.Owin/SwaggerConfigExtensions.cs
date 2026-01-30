namespace Voyager.Common.Proxy.Server.Swagger.Owin;

using System;
using global::Swagger.Net.Application;

/// <summary>
/// Extension methods for configuring Swagger.Net with service proxy support.
/// </summary>
public static class SwaggerConfigExtensions
{
    /// <summary>
    /// Adds a service proxy document filter to Swagger generation.
    /// This will add all endpoints from the specified service interface to the Swagger document.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="config">The Swagger docs config.</param>
    /// <returns>The config for chaining.</returns>
    /// <example>
    /// <code>
    /// GlobalConfiguration.Configuration
    ///     .EnableSwagger(c =>
    ///     {
    ///         c.SingleApiVersion("v1", "My API");
    ///         c.AddServiceProxy&lt;IUserService&gt;();
    ///         c.AddServiceProxy&lt;IOrderService&gt;();
    ///     })
    ///     .EnableSwaggerUi();
    /// </code>
    /// </example>
    public static SwaggerDocsConfig AddServiceProxy<TService>(this SwaggerDocsConfig config)
        where TService : class
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        config.DocumentFilter<ServiceProxyDocumentFilter<TService>>();
        return config;
    }
}
