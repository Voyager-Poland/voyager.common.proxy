namespace Voyager.Common.Proxy.Server.Owin;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Server.Abstractions;
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
    private readonly Dictionary<EndpointDescriptor, AuthorizationInfo> _authorizationCache;

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
        _authorizationCache = new Dictionary<EndpointDescriptor, AuthorizationInfo>();
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

        // Check authorization
        var authInfo = GetAuthorizationInfo(endpoint);
        var authResult = AuthorizationChecker.CheckAuthorization(environment, authInfo);

        if (!authResult.Succeeded)
        {
            await WriteAuthorizationFailureResponse(environment, authResult).ConfigureAwait(false);
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

    private AuthorizationInfo GetAuthorizationInfo(EndpointDescriptor endpoint)
    {
        if (!_authorizationCache.TryGetValue(endpoint, out var authInfo))
        {
            authInfo = AuthorizationChecker.GetAuthorizationInfo(endpoint);
            _authorizationCache[endpoint] = authInfo;
        }
        return authInfo;
    }

    private static async Task WriteAuthorizationFailureResponse(
        IDictionary<string, object> environment,
        AuthorizationResult authResult)
    {
        var statusCode = authResult.IsUnauthorized ? 401 : 403;
        var reasonPhrase = authResult.IsUnauthorized ? "Unauthorized" : "Forbidden";

        environment["owin.ResponseStatusCode"] = statusCode;
        environment["owin.ResponseReasonPhrase"] = reasonPhrase;

        var headers = environment["owin.ResponseHeaders"] as IDictionary<string, string[]>;
        if (headers != null)
        {
            headers["Content-Type"] = new[] { "application/json; charset=utf-8" };
        }

        var responseBody = environment["owin.ResponseBody"] as Stream;
        if (responseBody != null)
        {
            var json = $"{{\"error\":\"{authResult.FailureReason}\"}}";
            var bytes = Encoding.UTF8.GetBytes(json);
            await responseBody.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }
    }
}
