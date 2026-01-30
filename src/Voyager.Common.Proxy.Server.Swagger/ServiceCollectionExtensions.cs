namespace Voyager.Common.Proxy.Server.Swagger;

using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.SwaggerGen;

/// <summary>
/// Extension methods for configuring Swagger with service proxy support.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a service proxy document filter to Swagger generation.
    /// This will add all endpoints from the specified service interface to the Swagger document.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="options">The Swagger generation options.</param>
    /// <returns>The options for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddSwaggerGen(c =>
    /// {
    ///     c.AddServiceProxy&lt;IUserService&gt;();
    ///     c.AddServiceProxy&lt;IOrderService&gt;();
    /// });
    /// </code>
    /// </example>
    public static SwaggerGenOptions AddServiceProxy<TService>(this SwaggerGenOptions options)
        where TService : class
    {
        options.DocumentFilter<ServiceProxyDocumentFilter<TService>>();
        return options;
    }

    /// <summary>
    /// Configures Swagger generation with service proxy support for the specified services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional additional configuration for Swagger.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddServiceProxySwagger(options =>
    /// {
    ///     options.SwaggerDoc("v1", new() { Title = "My API", Version = "v1" });
    ///     options.AddServiceProxy&lt;IUserService&gt;();
    ///     options.AddServiceProxy&lt;IOrderService&gt;();
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddServiceProxySwagger(
        this IServiceCollection services,
        Action<SwaggerGenOptions>? configure = null)
    {
        services.AddSwaggerGen(options =>
        {
            configure?.Invoke(options);
        });

        return services;
    }
}
