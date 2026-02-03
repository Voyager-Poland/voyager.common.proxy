namespace Voyager.Common.Proxy.Server.Owin;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Diagnostics;
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
    private readonly ParameterBinder _parameterBinder;
    private readonly Dictionary<EndpointDescriptor, AuthorizationInfo> _authorizationCache;

    // Factory options - one of these will be set
    private readonly Func<TService>? _serviceFactory;
    private readonly Func<IDictionary<string, object>, TService>? _contextAwareFactory;

    // Permission checking - optional
    private readonly Func<PermissionContext, Task<PermissionResult>>? _permissionChecker;

    // Diagnostics - optional
    private readonly IEnumerable<IProxyDiagnostics>? _diagnosticsHandlers;
    private readonly Func<IDictionary<string, object>, IProxyRequestContext?>? _requestContextFactory;

    /// <summary>
    /// Creates a new instance of the service proxy middleware with a simple factory.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="matcher">The endpoint matcher for routing requests.</param>
    /// <param name="serviceFactory">Factory function to create service instances.</param>
    public ServiceProxyMiddleware(
        AppFunc next,
        EndpointMatcher matcher,
        Func<TService> serviceFactory)
        : this(next, matcher, serviceFactory, null, null, null, null)
    {
    }

    /// <summary>
    /// Creates a new instance of the service proxy middleware with a context-aware factory.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="matcher">The endpoint matcher for routing requests.</param>
    /// <param name="contextAwareFactory">Factory function that receives the OWIN environment.</param>
    public ServiceProxyMiddleware(
        AppFunc next,
        EndpointMatcher matcher,
        Func<IDictionary<string, object>, TService> contextAwareFactory)
        : this(next, matcher, null, contextAwareFactory, null, null, null)
    {
    }

    /// <summary>
    /// Creates a new instance of the service proxy middleware with full options.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="matcher">The endpoint matcher for routing requests.</param>
    /// <param name="options">The middleware configuration options.</param>
    public ServiceProxyMiddleware(
        AppFunc next,
        EndpointMatcher matcher,
        ServiceProxyOptions<TService> options)
        : this(
            next,
            matcher,
            options.ServiceFactory,
            options.ContextAwareFactory,
            options.GetEffectivePermissionChecker(),
            options.DiagnosticsHandlers,
            options.RequestContextFactory)
    {
    }

    private ServiceProxyMiddleware(
        AppFunc next,
        EndpointMatcher matcher,
        Func<TService>? serviceFactory,
        Func<IDictionary<string, object>, TService>? contextAwareFactory,
        Func<PermissionContext, Task<PermissionResult>>? permissionChecker,
        IEnumerable<IProxyDiagnostics>? diagnosticsHandlers,
        Func<IDictionary<string, object>, IProxyRequestContext?>? requestContextFactory)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _serviceFactory = serviceFactory;
        _contextAwareFactory = contextAwareFactory;
        _permissionChecker = permissionChecker;
        _diagnosticsHandlers = diagnosticsHandlers;
        _requestContextFactory = requestContextFactory;
        _dispatcher = new RequestDispatcher();
        _parameterBinder = new ParameterBinder();
        _authorizationCache = new Dictionary<EndpointDescriptor, AuthorizationInfo>();

        if (serviceFactory == null && contextAwareFactory == null)
        {
            throw new ArgumentException(
                "Either serviceFactory or contextAwareFactory must be provided.");
        }
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

        // Step 1: Check attribute-based authorization ([RequireAuthorization])
        var authInfo = GetAuthorizationInfo(endpoint);
        var authResult = AuthorizationChecker.CheckAuthorization(environment, authInfo);

        if (!authResult.Succeeded)
        {
            await WriteAuthorizationFailureResponse(environment, authResult).ConfigureAwait(false);
            return;
        }

        // Step 2: Check custom permission checker (if configured)
        if (_permissionChecker != null)
        {
            var requestContext = new OwinRequestContext(environment, routeValues);

            // Bind parameters for permission context
            var parameterValues = await _parameterBinder.BindParametersAsync(requestContext, endpoint)
                .ConfigureAwait(false);
            var parameterDict = BuildParameterDictionary(endpoint, parameterValues);

            var permissionContext = new PermissionContext(
                user: GetUser(environment),
                serviceType: endpoint.ServiceType,
                method: endpoint.Method,
                endpoint: endpoint,
                parameters: parameterDict,
                rawContext: environment);

            var permissionResult = await _permissionChecker(permissionContext).ConfigureAwait(false);

            if (!permissionResult.IsGranted)
            {
                await WritePermissionFailureResponse(environment, permissionResult).ConfigureAwait(false);
                return;
            }
        }

        // Step 3: Create service and dispatch
        var service = CreateService(environment);
        var reqContext = new OwinRequestContext(environment, routeValues);
        var responseWriter = new OwinResponseWriter(environment);
        var proxyRequestContext = _requestContextFactory?.Invoke(environment);

        await _dispatcher.DispatchAsync(reqContext, responseWriter, endpoint, service, _diagnosticsHandlers, proxyRequestContext)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the OWIN middleware function.
    /// </summary>
    /// <returns>The middleware AppFunc delegate.</returns>
    public AppFunc ToAppFunc() => Invoke;

    private TService CreateService(IDictionary<string, object> environment)
    {
        if (_contextAwareFactory != null)
        {
            return _contextAwareFactory(environment);
        }

        return _serviceFactory!();
    }

    private AuthorizationInfo GetAuthorizationInfo(EndpointDescriptor endpoint)
    {
        if (!_authorizationCache.TryGetValue(endpoint, out var authInfo))
        {
            authInfo = AuthorizationChecker.GetAuthorizationInfo(endpoint);
            _authorizationCache[endpoint] = authInfo;
        }
        return authInfo;
    }

    private static IPrincipal? GetUser(IDictionary<string, object> environment)
    {
        // Try different keys where the user might be stored
        if (environment.TryGetValue("server.User", out var serverUser) && serverUser is IPrincipal principal1)
        {
            return principal1;
        }

        if (environment.TryGetValue("owin.User", out var owinUser) && owinUser is IPrincipal principal2)
        {
            return principal2;
        }

        // For Microsoft.Owin (Katana)
        if (environment.TryGetValue("Microsoft.Owin.Security.User", out var katanaUser) && katanaUser is IPrincipal principal3)
        {
            return principal3;
        }

        return null;
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

    private static async Task WriteAuthorizationFailureResponse(
        IDictionary<string, object> environment,
        AuthorizationResult authResult)
    {
        var statusCode = authResult.IsUnauthorized ? 401 : 403;
        var reasonPhrase = authResult.IsUnauthorized ? "Unauthorized" : "Forbidden";

        await WriteErrorResponse(environment, statusCode, reasonPhrase, authResult.FailureReason ?? "Access denied")
            .ConfigureAwait(false);
    }

    private static async Task WritePermissionFailureResponse(
        IDictionary<string, object> environment,
        PermissionResult permissionResult)
    {
        var statusCode = permissionResult.IsAuthenticationFailure ? 401 : 403;
        var reasonPhrase = permissionResult.IsAuthenticationFailure ? "Unauthorized" : "Forbidden";

        await WriteErrorResponse(environment, statusCode, reasonPhrase, permissionResult.DenialReason ?? "Permission denied")
            .ConfigureAwait(false);
    }

    private static async Task WriteErrorResponse(
        IDictionary<string, object> environment,
        int statusCode,
        string reasonPhrase,
        string errorMessage)
    {
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
            // Escape the error message for JSON
            var escapedMessage = errorMessage
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");

            var json = $"{{\"error\":\"{escapedMessage}\"}}";
            var bytes = Encoding.UTF8.GetBytes(json);
            await responseBody.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }
    }
}
