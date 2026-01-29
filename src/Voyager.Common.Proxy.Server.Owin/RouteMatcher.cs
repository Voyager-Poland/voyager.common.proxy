namespace Voyager.Common.Proxy.Server.Owin;

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Matches request paths against route templates and extracts route values.
/// </summary>
internal sealed class RouteMatcher
{
    private readonly Regex _routeRegex;
    private readonly string[] _parameterNames;

    public RouteMatcher(string routeTemplate)
    {
        var pattern = BuildRegexPattern(routeTemplate, out var paramNames);
        _routeRegex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        _parameterNames = paramNames;
    }

    /// <summary>
    /// Attempts to match a request path against the route template.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <param name="routeValues">The extracted route values if matched.</param>
    /// <returns>True if the path matches; otherwise, false.</returns>
    public bool TryMatch(string path, out IDictionary<string, string> routeValues)
    {
        routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var match = _routeRegex.Match(path);
        if (!match.Success)
        {
            return false;
        }

        for (int i = 0; i < _parameterNames.Length; i++)
        {
            var group = match.Groups[i + 1];
            if (group.Success)
            {
                routeValues[_parameterNames[i]] = Uri.UnescapeDataString(group.Value);
            }
        }

        return true;
    }

    private static string BuildRegexPattern(string routeTemplate, out string[] parameterNames)
    {
        var paramList = new List<string>();

        // Replace {param} or {param:constraint} with regex capture groups
        var pattern = Regex.Replace(
            routeTemplate,
            @"\{(\w+)(?::[^}]+)?\}",
            match =>
            {
                paramList.Add(match.Groups[1].Value);
                return "([^/]+)";
            });

        // Escape other regex special characters and anchor the pattern
        pattern = "^" + Regex.Escape(pattern).Replace("\\(\\[\\^/\\]\\+\\)", "([^/]+)") + "$";

        // Fix the double escaping issue - rebuild properly
        pattern = "^" + Regex.Replace(
            routeTemplate,
            @"\{(\w+)(?::[^}]+)?\}",
            _ => "([^/]+)") + "$";

        parameterNames = paramList.ToArray();
        return pattern;
    }
}

/// <summary>
/// Maps endpoints with their route matchers.
/// </summary>
internal sealed class EndpointMatcher
{
    private readonly List<(EndpointDescriptor Descriptor, RouteMatcher Matcher)> _endpoints;

    public EndpointMatcher(IEnumerable<EndpointDescriptor> endpoints)
    {
        _endpoints = new List<(EndpointDescriptor, RouteMatcher)>();

        foreach (var endpoint in endpoints)
        {
            _endpoints.Add((endpoint, new RouteMatcher(endpoint.RouteTemplate)));
        }
    }

    /// <summary>
    /// Finds a matching endpoint for the given HTTP method and path.
    /// </summary>
    public (EndpointDescriptor? Endpoint, IDictionary<string, string> RouteValues) Match(string httpMethod, string path)
    {
        foreach (var (descriptor, matcher) in _endpoints)
        {
            if (!string.Equals(descriptor.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (matcher.TryMatch(path, out var routeValues))
            {
                return (descriptor, routeValues);
            }
        }

        return (null, new Dictionary<string, string>());
    }
}
