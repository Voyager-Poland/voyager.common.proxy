namespace Voyager.Common.Proxy.Server.Abstractions;

using System.Collections.Generic;
using System.IO;
using System.Threading;

/// <summary>
/// Abstracts the HTTP request context across different platforms (ASP.NET Core, OWIN).
/// </summary>
public interface IRequestContext
{
    /// <summary>
    /// Gets the HTTP method of the request (GET, POST, PUT, DELETE).
    /// </summary>
    string HttpMethod { get; }

    /// <summary>
    /// Gets the request path.
    /// </summary>
    string Path { get; }

    /// <summary>
    /// Gets the route values extracted from the route template.
    /// </summary>
    IReadOnlyDictionary<string, string> RouteValues { get; }

    /// <summary>
    /// Gets the query string parameters.
    /// </summary>
    IReadOnlyDictionary<string, string> QueryParameters { get; }

    /// <summary>
    /// Gets the request body stream.
    /// </summary>
    Stream Body { get; }

    /// <summary>
    /// Gets the cancellation token for the request.
    /// </summary>
    CancellationToken CancellationToken { get; }
}
