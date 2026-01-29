namespace Voyager.Common.Proxy.Server.Abstractions;

using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Describes an HTTP endpoint generated from a service interface method.
/// </summary>
public sealed class EndpointDescriptor
{
    /// <summary>
    /// Gets the service interface type.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// Gets the method that this endpoint invokes.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// Gets the HTTP method (GET, POST, PUT, DELETE).
    /// </summary>
    public string HttpMethod { get; }

    /// <summary>
    /// Gets the route template (e.g., "/api/users/{id}").
    /// </summary>
    public string RouteTemplate { get; }

    /// <summary>
    /// Gets the parameter descriptors for this endpoint.
    /// </summary>
    public IReadOnlyList<ParameterDescriptor> Parameters { get; }

    /// <summary>
    /// Gets the return type of the method (unwrapped from Task).
    /// </summary>
    public Type ReturnType { get; }

    /// <summary>
    /// Gets the inner type of Result&lt;T&gt; or null for Result (non-generic).
    /// </summary>
    public Type? ResultValueType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EndpointDescriptor"/> class.
    /// </summary>
    public EndpointDescriptor(
        Type serviceType,
        MethodInfo method,
        string httpMethod,
        string routeTemplate,
        IReadOnlyList<ParameterDescriptor> parameters,
        Type returnType,
        Type? resultValueType)
    {
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
        Method = method ?? throw new ArgumentNullException(nameof(method));
        HttpMethod = httpMethod ?? throw new ArgumentNullException(nameof(httpMethod));
        RouteTemplate = routeTemplate ?? throw new ArgumentNullException(nameof(routeTemplate));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
        ResultValueType = resultValueType;
    }
}
