namespace Voyager.Common.Proxy.Server.AspNetCore;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Voyager.Common.Proxy.Server.Core;

/// <summary>
/// Extension methods for mapping service proxy endpoints.
/// </summary>
public static class ServiceProxyEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps all methods from a service interface as HTTP endpoints.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <example>
    /// <code>
    /// app.MapServiceProxy&lt;IUserService&gt;();
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapServiceProxy<TService>(this IEndpointRouteBuilder endpoints)
        where TService : class
    {
        return MapServiceProxy<TService>(endpoints, _ => { });
    }

    /// <summary>
    /// Maps all methods from a service interface as HTTP endpoints with configuration.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">A delegate to configure endpoint conventions.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapServiceProxy<TService>(
        this IEndpointRouteBuilder endpoints,
        Action<IEndpointConventionBuilder> configure)
        where TService : class
    {
        var scanner = new ServiceScanner();
        var descriptors = scanner.ScanInterface<TService>();
        var dispatcher = new RequestDispatcher();

        foreach (var descriptor in descriptors)
        {
            var builder = endpoints.MapMethods(
                descriptor.RouteTemplate,
                new[] { descriptor.HttpMethod },
                async (HttpContext context) =>
                {
                    var service = context.RequestServices.GetRequiredService<TService>();
                    var requestContext = new AspNetCoreRequestContext(context);
                    var responseWriter = new AspNetCoreResponseWriter(context.Response);

                    await dispatcher.DispatchAsync(requestContext, responseWriter, descriptor, service);
                });

            // Add metadata for OpenAPI/Swagger
            builder.WithMetadata(new ServiceProxyEndpointMetadata(
                descriptor.ServiceType,
                descriptor.Method,
                descriptor.ReturnType,
                descriptor.ResultValueType));

            configure(builder);
        }

        return endpoints;
    }
}

/// <summary>
/// Metadata attached to service proxy endpoints for discovery.
/// </summary>
public sealed class ServiceProxyEndpointMetadata
{
    /// <summary>
    /// Gets the service interface type.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// Gets the method info.
    /// </summary>
    public System.Reflection.MethodInfo Method { get; }

    /// <summary>
    /// Gets the return type.
    /// </summary>
    public Type ReturnType { get; }

    /// <summary>
    /// Gets the result value type (for Result&lt;T&gt;) or null.
    /// </summary>
    public Type? ResultValueType { get; }

    internal ServiceProxyEndpointMetadata(
        Type serviceType,
        System.Reflection.MethodInfo method,
        Type returnType,
        Type? resultValueType)
    {
        ServiceType = serviceType;
        Method = method;
        ReturnType = returnType;
        ResultValueType = resultValueType;
    }
}
