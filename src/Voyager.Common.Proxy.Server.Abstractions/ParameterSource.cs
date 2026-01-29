namespace Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Specifies the source of a parameter value in an HTTP request.
/// </summary>
public enum ParameterSource
{
    /// <summary>
    /// Parameter value comes from the route template (e.g., /users/{id}).
    /// </summary>
    Route,

    /// <summary>
    /// Parameter value comes from the query string (e.g., ?name=value).
    /// </summary>
    Query,

    /// <summary>
    /// Parameter value comes from the request body (JSON deserialization).
    /// </summary>
    Body,

    /// <summary>
    /// Parameter is a CancellationToken provided by the framework.
    /// </summary>
    CancellationToken,

    /// <summary>
    /// Parameter is a complex type bound from route values and query string.
    /// Route values take precedence over query string for matching properties.
    /// </summary>
    RouteAndQuery
}
