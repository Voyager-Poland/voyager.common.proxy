namespace Voyager.Common.Proxy.Server.AspNetCore;

using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Voyager.Common.Proxy.Abstractions;
using Voyager.Common.Proxy.Server.Core;
using ProxyAllowAnonymous = Voyager.Common.Proxy.Abstractions.AllowAnonymousAttribute;
using AspNetAllowAnonymous = Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute;

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
    /// <example>
    /// <code>
    /// // Apply authorization to all endpoints
    /// app.MapServiceProxy&lt;IUserService&gt;(e => e.RequireAuthorization());
    ///
    /// // Apply specific policy
    /// app.MapServiceProxy&lt;IAdminService&gt;(e => e.RequireAuthorization("AdminPolicy"));
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapServiceProxy<TService>(
        this IEndpointRouteBuilder endpoints,
        Action<IEndpointConventionBuilder> configure)
        where TService : class
    {
        var serviceType = typeof(TService);
        var scanner = new ServiceScanner();
        var descriptors = scanner.ScanInterface<TService>();
        var dispatcher = new RequestDispatcher();

        // Get interface-level authorization attributes
        var interfaceAuthAttributes = GetAuthorizationData(serviceType);

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

            // Apply authorization from attributes
            ApplyAuthorization(builder, descriptor.Method, interfaceAuthAttributes);

            configure(builder);
        }

        return endpoints;
    }

    private static void ApplyAuthorization(
        IEndpointConventionBuilder builder,
        MethodInfo method,
        IReadOnlyList<IAuthorizeData> interfaceAuthAttributes)
    {
        // Check for AllowAnonymous on method (both our attribute and ASP.NET Core's)
        var hasAllowAnonymous = method.GetCustomAttribute<ProxyAllowAnonymous>() != null ||
                                method.GetCustomAttribute<AspNetAllowAnonymous>() != null;

        if (hasAllowAnonymous)
        {
            builder.AllowAnonymous();
            return;
        }

        // Get method-level authorization attributes
        var methodAuthAttributes = GetAuthorizationData(method);

        // If method has its own authorization, use that
        if (methodAuthAttributes.Count > 0)
        {
            foreach (var authData in methodAuthAttributes)
            {
                ApplyAuthorizationData(builder, authData);
            }
            return;
        }

        // Otherwise, use interface-level authorization
        if (interfaceAuthAttributes.Count > 0)
        {
            foreach (var authData in interfaceAuthAttributes)
            {
                ApplyAuthorizationData(builder, authData);
            }
        }
    }

    private static IReadOnlyList<IAuthorizeData> GetAuthorizationData(MemberInfo memberInfo)
    {
        var result = new List<IAuthorizeData>();

        // Check for our RequireAuthorizationAttribute
        foreach (var attr in memberInfo.GetCustomAttributes<RequireAuthorizationAttribute>())
        {
            result.Add(new AuthorizeDataAdapter(attr));
        }

        // Check for ASP.NET Core's AuthorizeAttribute
        foreach (var attr in memberInfo.GetCustomAttributes<AuthorizeAttribute>())
        {
            result.Add(attr);
        }

        return result;
    }

    private static void ApplyAuthorizationData(IEndpointConventionBuilder builder, IAuthorizeData authData)
    {
        if (!string.IsNullOrEmpty(authData.Policy))
        {
            builder.RequireAuthorization(authData.Policy);
        }
        else if (!string.IsNullOrEmpty(authData.Roles) || !string.IsNullOrEmpty(authData.AuthenticationSchemes))
        {
            builder.RequireAuthorization(new AuthorizeAttribute
            {
                Roles = authData.Roles,
                AuthenticationSchemes = authData.AuthenticationSchemes
            });
        }
        else
        {
            builder.RequireAuthorization();
        }
    }

    /// <summary>
    /// Adapter to convert RequireAuthorizationAttribute to IAuthorizeData.
    /// </summary>
    private sealed class AuthorizeDataAdapter : IAuthorizeData
    {
        public string? Policy { get; set; }
        public string? Roles { get; set; }
        public string? AuthenticationSchemes { get; set; }

        public AuthorizeDataAdapter(RequireAuthorizationAttribute attr)
        {
            Policy = attr.Policy;
            Roles = attr.Roles;
            AuthenticationSchemes = attr.AuthenticationSchemes;
        }
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
    public MethodInfo Method { get; }

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
        MethodInfo method,
        Type returnType,
        Type? resultValueType)
    {
        ServiceType = serviceType;
        Method = method;
        ReturnType = returnType;
        ResultValueType = resultValueType;
    }
}
