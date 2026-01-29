namespace Voyager.Common.Proxy.Server.AspNetCore;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Adapts ASP.NET Core HttpContext to IRequestContext.
/// </summary>
internal sealed class AspNetCoreRequestContext : IRequestContext
{
    private readonly HttpContext _context;
    private readonly IReadOnlyDictionary<string, string> _routeValues;
    private readonly IReadOnlyDictionary<string, string> _queryParameters;

    public AspNetCoreRequestContext(HttpContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _routeValues = BuildRouteValues(context);
        _queryParameters = BuildQueryParameters(context);
    }

    public string HttpMethod => _context.Request.Method;

    public string Path => _context.Request.Path.Value ?? "/";

    public IReadOnlyDictionary<string, string> RouteValues => _routeValues;

    public IReadOnlyDictionary<string, string> QueryParameters => _queryParameters;

    public Stream Body => _context.Request.Body;

    public CancellationToken CancellationToken => _context.RequestAborted;

    private static IReadOnlyDictionary<string, string> BuildRouteValues(HttpContext context)
    {
        var routeData = context.GetRouteData();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (routeData?.Values != null)
        {
            foreach (var kvp in routeData.Values)
            {
                if (kvp.Value != null)
                {
                    result[kvp.Key] = kvp.Value.ToString()!;
                }
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, string> BuildQueryParameters(HttpContext context)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in context.Request.Query)
        {
            result[kvp.Key] = kvp.Value.ToString();
        }

        return result;
    }
}
