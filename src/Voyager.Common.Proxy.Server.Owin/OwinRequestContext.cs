namespace Voyager.Common.Proxy.Server.Owin;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Adapts the OWIN environment dictionary to IRequestContext.
/// </summary>
/// <remarks>
/// Uses raw OWIN environment keys for maximum compatibility:
/// - owin.RequestMethod: HTTP method (GET, POST, etc.)
/// - owin.RequestPath: Request path
/// - owin.RequestQueryString: Query string (without leading ?)
/// - owin.RequestBody: Request body stream
/// - owin.CallCancelled: Cancellation token
/// </remarks>
internal sealed class OwinRequestContext : IRequestContext
{
    private readonly IDictionary<string, object> _environment;
    private readonly IReadOnlyDictionary<string, string> _routeValues;
    private readonly IReadOnlyDictionary<string, string> _queryParameters;

    /// <summary>
    /// Creates a new OWIN request context.
    /// </summary>
    /// <param name="environment">The OWIN environment dictionary.</param>
    /// <param name="routeValues">Route values extracted from the URL path.</param>
    public OwinRequestContext(IDictionary<string, object> environment, IDictionary<string, string> routeValues)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _routeValues = new Dictionary<string, string>(routeValues ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        _queryParameters = ParseQueryString(GetEnvironmentValue<string>("owin.RequestQueryString"));
    }

    /// <inheritdoc />
    public string HttpMethod => GetEnvironmentValue<string>("owin.RequestMethod") ?? "";

    /// <inheritdoc />
    public string Path => GetEnvironmentValue<string>("owin.RequestPath") ?? "/";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> RouteValues => _routeValues;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> QueryParameters => _queryParameters;

    /// <inheritdoc />
    public Stream Body => GetEnvironmentValue<Stream>("owin.RequestBody") ?? Stream.Null;

    /// <inheritdoc />
    public CancellationToken CancellationToken => GetEnvironmentValue<CancellationToken>("owin.CallCancelled");

    private T? GetEnvironmentValue<T>(string key)
    {
        if (_environment.TryGetValue(key, out var value) && value is T typed)
        {
            return typed;
        }
        return default;
    }

    private static IReadOnlyDictionary<string, string> ParseQueryString(string? queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(queryString))
        {
            return result;
        }

        // Parse query string: key1=value1&key2=value2
        // queryString is guaranteed non-null after the IsNullOrEmpty check above
        var pairs = queryString!.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split(new[] { '=' }, 2);
            if (parts.Length > 0)
            {
                var key = Uri.UnescapeDataString(parts[0]);
                var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
                result[key] = value;
            }
        }

        return result;
    }
}
