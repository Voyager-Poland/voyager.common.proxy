namespace Voyager.Common.Proxy.Server.Owin;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Server.Core;

// OWIN delegate type alias
using AppFunc = System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;

/// <summary>
/// Extension methods and factory for creating service proxy OWIN middleware.
/// </summary>
/// <remarks>
/// This implementation uses raw OWIN delegate signatures to avoid reference resolution
/// issues with the Owin package in SDK-style projects. The middleware can be used with
/// any OWIN host (Katana, Microsoft.Owin, etc.) by using the standard middleware pattern.
/// </remarks>
public static class ServiceProxyOwinMiddleware
{
    /// <summary>
    /// Creates OWIN middleware that handles requests for the specified service interface.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="serviceFactory">A factory function that creates service instances.</param>
    /// <returns>A function that wraps the next middleware with service proxy handling.</returns>
    /// <example>
    /// <code>
    /// // In Startup.cs with Microsoft.Owin
    /// public class Startup
    /// {
    ///     public void Configuration(IAppBuilder app)
    ///     {
    ///         var container = new UnityContainer();
    ///         container.RegisterType&lt;IUserService, UserService&gt;();
    ///
    ///         // Use the middleware factory
    ///         app.Use(ServiceProxyOwinMiddleware.Create&lt;IUserService&gt;(
    ///             () => container.Resolve&lt;IUserService&gt;()));
    ///     }
    /// }
    /// </code>
    /// </example>
    public static Func<AppFunc, AppFunc> Create<TService>(Func<TService> serviceFactory)
        where TService : class
    {
        if (serviceFactory == null)
        {
            throw new ArgumentNullException(nameof(serviceFactory));
        }

        var scanner = new ServiceScanner();
        var endpoints = scanner.ScanInterface<TService>();
        var matcher = new EndpointMatcher(endpoints);

        return next =>
        {
            var middleware = new ServiceProxyMiddleware<TService>(next, matcher, serviceFactory);
            return middleware.ToAppFunc();
        };
    }

    /// <summary>
    /// Creates OWIN middleware that handles requests for the specified service interface,
    /// resolving the service from a service provider.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="serviceProvider">The service provider to resolve services from.</param>
    /// <returns>A function that wraps the next middleware with service proxy handling.</returns>
    /// <example>
    /// <code>
    /// // In Startup.cs with Microsoft.Owin
    /// public class Startup
    /// {
    ///     public void Configuration(IAppBuilder app)
    ///     {
    ///         var services = new ServiceCollection();
    ///         services.AddTransient&lt;IUserService, UserService&gt;();
    ///         var provider = services.BuildServiceProvider();
    ///
    ///         // Use the middleware factory with service provider
    ///         app.Use(ServiceProxyOwinMiddleware.Create&lt;IUserService&gt;(provider));
    ///     }
    /// }
    /// </code>
    /// </example>
    public static Func<AppFunc, AppFunc> Create<TService>(IServiceProvider serviceProvider)
        where TService : class
    {
        if (serviceProvider == null)
        {
            throw new ArgumentNullException(nameof(serviceProvider));
        }

        return Create<TService>(() =>
        {
            var service = serviceProvider.GetService(typeof(TService)) as TService;
            if (service == null)
            {
                throw new InvalidOperationException(
                    $"Service of type {typeof(TService).Name} is not registered in the service provider.");
            }
            return service;
        });
    }

    /// <summary>
    /// Creates OWIN middleware with a context-aware service factory.
    /// Use this overload when your service needs access to the OWIN request context
    /// (e.g., to create per-request identity from user claims).
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="contextAwareFactory">
    /// A factory function that receives the OWIN environment dictionary and returns a service instance.
    /// </param>
    /// <returns>A function that wraps the next middleware with service proxy handling.</returns>
    /// <example>
    /// <code>
    /// app.Use(ServiceProxyOwinMiddleware.Create&lt;IVIPService&gt;(env =>
    /// {
    ///     var user = env["server.User"] as ClaimsPrincipal;
    ///     var identity = PilotIdentityFactory.Create(user);
    ///     return new VIPService(identity, actionModule);
    /// }));
    /// </code>
    /// </example>
    public static Func<AppFunc, AppFunc> Create<TService>(
        Func<IDictionary<string, object>, TService> contextAwareFactory)
        where TService : class
    {
        if (contextAwareFactory == null)
        {
            throw new ArgumentNullException(nameof(contextAwareFactory));
        }

        var scanner = new ServiceScanner();
        var endpoints = scanner.ScanInterface<TService>();
        var matcher = new EndpointMatcher(endpoints);

        return next =>
        {
            var middleware = new ServiceProxyMiddleware<TService>(next, matcher, contextAwareFactory);
            return middleware.ToAppFunc();
        };
    }

    /// <summary>
    /// Creates OWIN middleware with full configuration options including permission checking.
    /// Use this overload when you need fine-grained control over service creation and authorization.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <param name="configure">An action to configure the middleware options.</param>
    /// <returns>A function that wraps the next middleware with service proxy handling.</returns>
    /// <example>
    /// <code>
    /// // Simple permission checking with inline callback
    /// app.Use(ServiceProxyOwinMiddleware.Create&lt;IVIPService&gt;(options =>
    /// {
    ///     options.ServiceFactory = () => container.Resolve&lt;IVIPService&gt;();
    ///     options.PermissionChecker = async ctx =>
    ///     {
    ///         if (ctx.User?.Identity?.IsAuthenticated != true)
    ///             return PermissionResult.Unauthenticated();
    ///
    ///         var identity = PilotIdentity.FromPrincipal(ctx.User);
    ///         var checker = container.Resolve&lt;IVIPPermissionChecker&gt;();
    ///         return await checker.CheckAsync(identity, ctx.Method.Name, ctx.Parameters);
    ///     };
    /// }));
    ///
    /// // Using a typed permission checker
    /// app.Use(ServiceProxyOwinMiddleware.Create&lt;IVIPService&gt;(options =>
    /// {
    ///     options.ServiceFactory = () => container.Resolve&lt;IVIPService&gt;();
    ///     options.PermissionCheckerInstance = container.Resolve&lt;IServicePermissionChecker&lt;IVIPService&gt;&gt;();
    /// }));
    ///
    /// // Context-aware factory with permission checking
    /// app.Use(ServiceProxyOwinMiddleware.Create&lt;IVIPService&gt;(options =>
    /// {
    ///     options.ContextAwareFactory = env =>
    ///     {
    ///         var user = env["server.User"] as ClaimsPrincipal;
    ///         var identity = PilotIdentityFactory.Create(user);
    ///         return new VIPService(identity, actionModule);
    ///     };
    ///     options.PermissionChecker = async ctx =>
    ///     {
    ///         // Your permission logic here
    ///         return PermissionResult.Granted();
    ///     };
    /// }));
    /// </code>
    /// </example>
    public static Func<AppFunc, AppFunc> Create<TService>(
        Action<ServiceProxyOptions<TService>> configure)
        where TService : class
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new ServiceProxyOptions<TService>();
        configure(options);
        options.Validate();

        var scanner = new ServiceScanner();
        var endpoints = scanner.ScanInterface<TService>();
        var matcher = new EndpointMatcher(endpoints);

        return next =>
        {
            var middleware = new ServiceProxyMiddleware<TService>(next, matcher, options);
            return middleware.ToAppFunc();
        };
    }
}
