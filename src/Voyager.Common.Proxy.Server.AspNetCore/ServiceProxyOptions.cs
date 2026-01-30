namespace Voyager.Common.Proxy.Server.AspNetCore;

using System;
using Microsoft.AspNetCore.Http;
using Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Options for configuring ASP.NET Core service proxy endpoints.
/// </summary>
/// <typeparam name="TService">The service interface type.</typeparam>
public sealed class ServiceProxyOptions<TService> : ServiceProxyOptionsBase<TService>
    where TService : class
{
    /// <summary>
    /// Gets or sets a context-aware factory that receives the HttpContext.
    /// Use this when your service needs access to the HTTP request context
    /// (e.g., to create per-request identity from user claims).
    /// Takes precedence over <see cref="ServiceProxyOptionsBase{TService}.ServiceFactory"/> if both are set.
    /// </summary>
    /// <example>
    /// <code>
    /// options.ContextAwareFactory = httpContext =>
    /// {
    ///     var identity = pilotIdentityFactory.Create(httpContext.User);
    ///     var actionModule = httpContext.RequestServices.GetRequiredService&lt;ActionModule&gt;();
    ///     return new VIPService(identity, actionModule);
    /// };
    /// </code>
    /// </example>
    public Func<HttpContext, TService>? ContextAwareFactory { get; set; }

    /// <summary>
    /// Gets or sets whether to use the default DI-based service resolution.
    /// When true, the service is resolved from HttpContext.RequestServices.
    /// This is the default behavior when no factory is explicitly set.
    /// </summary>
    public bool UseDefaultServiceResolution { get; set; } = true;

    /// <summary>
    /// Validates the options configuration.
    /// </summary>
    public override void Validate()
    {
        base.Validate();

        // At least one service creation mechanism must be available
        // UseDefaultServiceResolution provides a fallback
    }

    /// <summary>
    /// Creates a service instance using the appropriate mechanism.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <returns>The service instance.</returns>
    internal TService CreateService(HttpContext httpContext)
    {
        if (ContextAwareFactory != null)
        {
            return ContextAwareFactory(httpContext);
        }

        if (ServiceFactory != null)
        {
            return ServiceFactory();
        }

        if (UseDefaultServiceResolution)
        {
            var service = httpContext.RequestServices.GetService(typeof(TService)) as TService;
            if (service == null)
            {
                throw new InvalidOperationException(
                    $"Service of type {typeof(TService).Name} is not registered in the service provider.");
            }
            return service;
        }

        throw new InvalidOperationException(
            "No service factory configured. Set ServiceFactory, ContextAwareFactory, or enable UseDefaultServiceResolution.");
    }
}
