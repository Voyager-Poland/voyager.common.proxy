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
}
