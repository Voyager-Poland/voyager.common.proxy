namespace Voyager.Common.Proxy.Server.Owin;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Server.Core;

// OWIN delegate type alias
using AppFunc = System.Func<System.Collections.Generic.IDictionary<string, object>, System.Threading.Tasks.Task>;

/// <summary>
/// OWIN middleware that handles requests for service proxy endpoints.
/// </summary>
/// <remarks>
/// This middleware uses the raw OWIN delegate signature (AppFunc) for maximum compatibility
/// and to avoid issues with IAppBuilder reference resolution in SDK-style projects.
/// </remarks>
internal sealed class ServiceProxyMiddleware<TService>
    where TService : class
{
    private readonly AppFunc _next;
    private readonly EndpointMatcher _matcher;
    private readonly RequestDispatcher _dispatcher;
    private readonly Func<TService> _serviceFactory;

    /// <summary>
    /// Creates a new instance of the service proxy middleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="matcher">The endpoint matcher for routing requests.</param>
    /// <param name="serviceFactory">Factory function to create service instances.</param>
    public ServiceProxyMiddleware(
        AppFunc next,
        EndpointMatcher matcher,
        Func<TService> serviceFactory)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _serviceFactory = serviceFactory ?? throw new ArgumentNullException(nameof(serviceFactory));
        _dispatcher = new RequestDispatcher();
    }

    /// <summary>
    /// Invokes the middleware for the given OWIN environment.
    /// </summary>
    /// <param name="environment">The OWIN environment dictionary.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Invoke(IDictionary<string, object> environment)
    {
        var method = environment["owin.RequestMethod"] as string ?? "";
        var path = environment["owin.RequestPath"] as string ?? "/";

        var (endpoint, routeValues) = _matcher.Match(method, path);

        if (endpoint == null)
        {
            await _next(environment).ConfigureAwait(false);
            return;
        }

        var service = _serviceFactory();
        var requestContext = new OwinRequestContext(environment, routeValues);
        var responseWriter = new OwinResponseWriter(environment);

        await _dispatcher.DispatchAsync(requestContext, responseWriter, endpoint, service)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the OWIN middleware function.
    /// </summary>
    /// <returns>The middleware AppFunc delegate.</returns>
    public AppFunc ToAppFunc() => Invoke;
}
