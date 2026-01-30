namespace Voyager.Common.Proxy.Server.Owin;

using System;
using System.Collections.Generic;
using Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Options for configuring OWIN service proxy middleware.
/// </summary>
/// <typeparam name="TService">The service interface type.</typeparam>
public sealed class ServiceProxyOptions<TService> : ServiceProxyOptionsBase<TService>
    where TService : class
{
    /// <summary>
    /// Gets or sets a context-aware factory that receives the OWIN environment dictionary.
    /// Use this when your service needs access to the request context (e.g., to create IPilotIdentity).
    /// Takes precedence over <see cref="ServiceProxyOptionsBase{TService}.ServiceFactory"/> if both are set.
    /// </summary>
    /// <example>
    /// <code>
    /// options.ContextAwareFactory = env =>
    /// {
    ///     var user = env["server.User"] as ClaimsPrincipal;
    ///     var identity = PilotIdentityFactory.Create(user);
    ///     return new VIPService(identity, actionModule);
    /// };
    /// </code>
    /// </example>
    public Func<IDictionary<string, object>, TService>? ContextAwareFactory { get; set; }

    /// <summary>
    /// Validates the options configuration.
    /// </summary>
    public override void Validate()
    {
        base.Validate();

        if (ServiceFactory == null && ContextAwareFactory == null)
        {
            throw new InvalidOperationException(
                "Either ServiceFactory or ContextAwareFactory must be set.");
        }
    }

    /// <summary>
    /// Creates a service instance using the appropriate factory.
    /// </summary>
    /// <param name="environment">The OWIN environment dictionary.</param>
    /// <returns>The service instance.</returns>
    internal TService CreateService(IDictionary<string, object> environment)
    {
        if (ContextAwareFactory != null)
        {
            return ContextAwareFactory(environment);
        }

        if (ServiceFactory != null)
        {
            return ServiceFactory();
        }

        throw new InvalidOperationException(
            "No service factory configured. Set either ServiceFactory or ContextAwareFactory.");
    }
}
