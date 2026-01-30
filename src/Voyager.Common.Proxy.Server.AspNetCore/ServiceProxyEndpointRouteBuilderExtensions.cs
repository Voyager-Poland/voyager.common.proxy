namespace Voyager.Common.Proxy.Server.AspNetCore;

using System.Reflection;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Voyager.Common.Proxy.Abstractions;
using Voyager.Common.Proxy.Server.Abstractions;
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
        return MapServiceProxy<TService>(endpoints, (Action<IEndpointConventionBuilder>)(_ => { }));
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

    /// <summary>
    /// Maps all methods from a service interface as HTTP endpoints with full options including permission checking.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configureOptions">A delegate to configure the service proxy options.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    /// <example>
    /// <code>
    /// // Simple permission checking with inline callback
    /// app.MapServiceProxy&lt;IVIPService&gt;(options =>
    /// {
    ///     options.PermissionChecker = async ctx =>
    ///     {
    ///         if (ctx.User?.Identity?.IsAuthenticated != true)
    ///             return PermissionResult.Unauthenticated();
    ///
    ///         var identity = PilotIdentity.FromPrincipal(ctx.User);
    ///         var checker = ((HttpContext)ctx.RawContext).RequestServices
    ///             .GetRequiredService&lt;IVIPPermissionChecker&gt;();
    ///         return await checker.CheckAsync(identity, ctx.Method.Name);
    ///     };
    /// });
    ///
    /// // Context-aware factory with permission checking
    /// app.MapServiceProxy&lt;IVIPService&gt;(options =>
    /// {
    ///     options.ContextAwareFactory = httpContext =>
    ///     {
    ///         var identity = pilotIdentityFactory.Create(httpContext.User);
    ///         return new VIPService(identity);
    ///     };
    ///     options.PermissionChecker = async ctx => PermissionResult.Granted();
    /// });
    /// </code>
    /// </example>
    public static IEndpointRouteBuilder MapServiceProxy<TService>(
        this IEndpointRouteBuilder endpoints,
        Action<ServiceProxyOptions<TService>> configureOptions)
        where TService : class
    {
        return MapServiceProxy<TService>(endpoints, configureOptions, _ => { });
    }

    /// <summary>
    /// Maps all methods from a service interface as HTTP endpoints with full options and endpoint configuration.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configureOptions">A delegate to configure the service proxy options.</param>
    /// <param name="configureEndpoints">A delegate to configure endpoint conventions.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapServiceProxy<TService>(
        this IEndpointRouteBuilder endpoints,
        Action<ServiceProxyOptions<TService>> configureOptions,
        Action<IEndpointConventionBuilder> configureEndpoints)
        where TService : class
    {
        var options = new ServiceProxyOptions<TService>();
        configureOptions(options);
        options.Validate();

        var serviceType = typeof(TService);
        var scanner = new ServiceScanner();
        var descriptors = scanner.ScanInterface<TService>();
        var dispatcher = new RequestDispatcher();
        var parameterBinder = new ParameterBinder();

        // Get interface-level authorization attributes
        var interfaceAuthAttributes = GetAuthorizationData(serviceType);

        // Get permission checker (may be null)
        var permissionChecker = options.GetEffectivePermissionChecker();

        foreach (var descriptor in descriptors)
        {
            var currentDescriptor = descriptor; // Capture for closure

            var builder = endpoints.MapMethods(
                descriptor.RouteTemplate,
                new[] { descriptor.HttpMethod },
                async (HttpContext context) =>
                {
                    // Step 1: Permission checking (if configured)
                    if (permissionChecker != null)
                    {
                        var requestContext = new AspNetCoreRequestContext(context);

                        // Bind parameters for permission context
                        var parameterValues = await parameterBinder.BindParametersAsync(
                            requestContext, currentDescriptor);
                        var parameterDict = BuildParameterDictionary(currentDescriptor, parameterValues);

                        var permissionContext = new PermissionContext(
                            user: context.User,
                            serviceType: currentDescriptor.ServiceType,
                            method: currentDescriptor.Method,
                            endpoint: currentDescriptor,
                            parameters: parameterDict,
                            rawContext: context);

                        var permissionResult = await permissionChecker(permissionContext);

                        if (!permissionResult.IsGranted)
                        {
                            await WritePermissionFailureResponse(context, permissionResult);
                            return;
                        }
                    }

                    // Step 2: Create service and dispatch
                    var service = options.CreateService(context);
                    var reqContext = new AspNetCoreRequestContext(context);
                    var responseWriter = new AspNetCoreResponseWriter(context.Response);

                    await dispatcher.DispatchAsync(reqContext, responseWriter, currentDescriptor, service);
                });

            // Add metadata for OpenAPI/Swagger
            builder.WithMetadata(new ServiceProxyEndpointMetadata(
                descriptor.ServiceType,
                descriptor.Method,
                descriptor.ReturnType,
                descriptor.ResultValueType));

            // Apply authorization from attributes
            ApplyAuthorization(builder, descriptor.Method, interfaceAuthAttributes);

            configureEndpoints(builder);
        }

        return endpoints;
    }

    private static Dictionary<string, object?> BuildParameterDictionary(
        EndpointDescriptor endpoint,
        object?[] parameterValues)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < endpoint.Parameters.Count && i < parameterValues.Length; i++)
        {
            var param = endpoint.Parameters[i];
            // Skip CancellationToken parameters
            if (param.Source != ParameterSource.CancellationToken)
            {
                dict[param.Name] = parameterValues[i];
            }
        }

        return dict;
    }

    private static async Task WritePermissionFailureResponse(
        HttpContext context,
        PermissionResult permissionResult)
    {
        var statusCode = permissionResult.IsAuthenticationFailure ? 401 : 403;
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var errorMessage = permissionResult.DenialReason ?? "Permission denied";
        // Escape for JSON
        errorMessage = errorMessage
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");

        await context.Response.WriteAsync($"{{\"error\":\"{errorMessage}\"}}");
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
