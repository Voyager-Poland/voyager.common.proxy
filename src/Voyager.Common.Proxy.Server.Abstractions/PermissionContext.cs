namespace Voyager.Common.Proxy.Server.Abstractions;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Principal;

/// <summary>
/// Context provided to permission checker before method invocation.
/// Contains all information needed to make an authorization decision.
/// </summary>
public sealed class PermissionContext
{
    /// <summary>
    /// Gets the authenticated user principal (may be null for anonymous requests).
    /// </summary>
    public IPrincipal? User { get; }

    /// <summary>
    /// Gets the service interface type being called.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// Gets the method being invoked.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// Gets the endpoint descriptor with route and parameter info.
    /// </summary>
    public EndpointDescriptor Endpoint { get; }

    /// <summary>
    /// Gets the deserialized request parameters.
    /// Key = parameter name, Value = parameter value.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// Gets the platform-specific raw context.
    /// For OWIN: IDictionary&lt;string, object&gt; (environment dictionary).
    /// For ASP.NET Core: HttpContext.
    /// </summary>
    public object RawContext { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionContext"/> class.
    /// </summary>
    public PermissionContext(
        IPrincipal? user,
        Type serviceType,
        MethodInfo method,
        EndpointDescriptor endpoint,
        IReadOnlyDictionary<string, object?> parameters,
        object rawContext)
    {
        User = user;
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        Method = method ?? throw new ArgumentNullException(nameof(method));
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        RawContext = rawContext ?? throw new ArgumentNullException(nameof(rawContext));
    }
}
