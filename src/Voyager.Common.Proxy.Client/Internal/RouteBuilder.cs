namespace Voyager.Common.Proxy.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Voyager.Common.Proxy.Abstractions;

    using ProxyHttpMethod = Voyager.Common.Proxy.Abstractions.HttpMethod;

    /// <summary>
    /// Builds HTTP routes from method metadata using conventions and attributes.
    /// </summary>
    internal static class RouteBuilder
    {
        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{(\w+)\}",
            RegexOptions.Compiled);

        /// <summary>
        /// Gets the HTTP method for the given method info.
        /// </summary>
        public static ProxyHttpMethod GetHttpMethod(MethodInfo method)
        {
            var attribute = method.GetCustomAttribute<HttpMethodAttribute>();
            if (attribute != null)
            {
                return attribute.Method;
            }

            // Convention-based detection
            var name = method.Name;

            if (name.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Find", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("List", StringComparison.OrdinalIgnoreCase))
            {
                return ProxyHttpMethod.Get;
            }

            if (name.StartsWith("Create", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Add", StringComparison.OrdinalIgnoreCase))
            {
                return ProxyHttpMethod.Post;
            }

            if (name.StartsWith("Update", StringComparison.OrdinalIgnoreCase))
            {
                return ProxyHttpMethod.Put;
            }

            if (name.StartsWith("Delete", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
            {
                return ProxyHttpMethod.Delete;
            }

            // Default to POST
            return ProxyHttpMethod.Post;
        }

        /// <summary>
        /// Gets the service route prefix from the interface.
        /// </summary>
        public static string GetServicePrefix(Type interfaceType)
        {
            var attribute = interfaceType.GetCustomAttribute<ServiceRouteAttribute>();
            if (attribute != null)
            {
                return attribute.Prefix;
            }

            // Convention: IUserService -> user-service
            var name = interfaceType.Name;
            if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
            {
                name = name.Substring(1);
            }

            return ToKebabCase(name);
        }

        /// <summary>
        /// Builds the full URL for a method call.
        /// </summary>
        public static (string url, object? body) BuildRequest(
            MethodInfo method,
            object?[] args,
            string servicePrefix)
        {
            var parameters = method.GetParameters();
            var attribute = method.GetCustomAttribute<HttpMethodAttribute>();
            var template = attribute?.Template;
            var httpMethod = GetHttpMethod(method);

            string path;
            var usedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            object? body = null;

            // For GET/DELETE with complex types, extract properties for route/query binding
            var propertyValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var value = args[i];

                if (param.ParameterType == typeof(CancellationToken))
                {
                    continue;
                }

                // For GET/DELETE: extract properties from complex types
                if (IsComplexType(param.ParameterType) &&
                    (httpMethod == ProxyHttpMethod.Get || httpMethod == ProxyHttpMethod.Delete) &&
                    value != null)
                {
                    foreach (var prop in param.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (prop.CanRead && !IsComplexType(prop.PropertyType))
                        {
                            var propValue = prop.GetValue(value);
                            if (propValue != null)
                            {
                                propertyValues[prop.Name] = propValue;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(template))
            {
                // Use template - extract placeholders
                path = template!;
                var matches = PlaceholderRegex.Matches(template);

                foreach (Match match in matches)
                {
                    var paramName = match.Groups[1].Value;
                    usedParameters.Add(paramName);

                    // First try method parameter
                    var paramIndex = FindParameterIndex(parameters, paramName);
                    if (paramIndex >= 0 && paramIndex < args.Length)
                    {
                        var value = args[paramIndex];
                        path = path.Replace(match.Value, Uri.EscapeDataString(value?.ToString() ?? ""));
                    }
                    // Then try properties from complex types (for GET/DELETE)
                    else if (propertyValues.TryGetValue(paramName, out var propValue))
                    {
                        path = path.Replace(match.Value, Uri.EscapeDataString(propValue?.ToString() ?? ""));
                    }
                }

                // Combine with service prefix
                path = CombinePaths(servicePrefix, path);
            }
            else
            {
                // Convention-based: method name to kebab-case
                var methodName = method.Name;
                if (methodName.EndsWith("Async", StringComparison.Ordinal))
                {
                    methodName = methodName.Substring(0, methodName.Length - 5);
                }

                path = CombinePaths(servicePrefix, ToKebabCase(methodName));
            }

            // Build query string and find body
            var queryParams = new List<string>();

            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var value = args[i];

                // Skip CancellationToken
                if (param.ParameterType == typeof(CancellationToken))
                {
                    continue;
                }

                // Skip parameters used in route template
                if (usedParameters.Contains(param.Name!))
                {
                    continue;
                }

                // Complex types become body (for POST, PUT, PATCH)
                if (IsComplexType(param.ParameterType) &&
                    (httpMethod == ProxyHttpMethod.Post ||
                     httpMethod == ProxyHttpMethod.Put ||
                     httpMethod == ProxyHttpMethod.Patch))
                {
                    body = value;
                }
                // For GET/DELETE with complex types: use extracted properties as query params
                else if (IsComplexType(param.ParameterType) &&
                         (httpMethod == ProxyHttpMethod.Get || httpMethod == ProxyHttpMethod.Delete) &&
                         value != null)
                {
                    foreach (var prop in param.ParameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (prop.CanRead && !IsComplexType(prop.PropertyType))
                        {
                            // Skip properties already used in route
                            if (usedParameters.Contains(prop.Name))
                            {
                                continue;
                            }

                            var propValue = prop.GetValue(value);
                            if (propValue != null)
                            {
                                queryParams.Add($"{Uri.EscapeDataString(prop.Name)}={Uri.EscapeDataString(propValue.ToString()!)}");
                            }
                        }
                    }
                }
                else if (value != null)
                {
                    // Simple types become query parameters
                    queryParams.Add($"{Uri.EscapeDataString(param.Name!)}={Uri.EscapeDataString(value.ToString()!)}");
                }
            }

            // Append query string
            if (queryParams.Count > 0)
            {
                path += "?" + string.Join("&", queryParams);
            }

            return (path, body);
        }

        private static int FindParameterIndex(ParameterInfo[] parameters, string name)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                if (string.Equals(parameters[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool IsComplexType(Type type)
        {
            // Nullable<T>
            var underlyingType = Nullable.GetUnderlyingType(type);
            if (underlyingType != null)
            {
                type = underlyingType;
            }

            // Primitives and common simple types
            if (type.IsPrimitive ||
                type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(TimeSpan) ||
                type == typeof(Guid) ||
                type.IsEnum)
            {
                return false;
            }

            return true;
        }

        private static string CombinePaths(string prefix, string path)
        {
            prefix = prefix.Trim('/');
            path = path.TrimStart('/');

            if (string.IsNullOrEmpty(prefix))
            {
                return "/" + path;
            }

            return "/" + prefix + "/" + path;
        }

        private static string ToKebabCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsUpper(c))
                {
                    if (i > 0)
                    {
                        sb.Append('-');
                    }
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
