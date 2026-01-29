namespace Voyager.Common.Proxy.Server.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Abstractions;
using Voyager.Common.Proxy.Server.Abstractions;

using ProxyHttpMethod = Voyager.Common.Proxy.Abstractions.HttpMethod;

/// <summary>
/// Scans service interfaces and builds endpoint descriptors.
/// </summary>
public class ServiceScanner
{
    private static readonly Regex PlaceholderRegex = new Regex(
        @"\{(\w+)(?::[^}]+)?\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Scans a service interface and returns endpoint descriptors for all methods.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <returns>A list of endpoint descriptors.</returns>
    public IReadOnlyList<EndpointDescriptor> ScanInterface<TService>()
        where TService : class
    {
        return ScanInterface(typeof(TService));
    }

    /// <summary>
    /// Scans a service interface and returns endpoint descriptors for all methods.
    /// </summary>
    /// <param name="serviceType">The service interface type.</param>
    /// <returns>A list of endpoint descriptors.</returns>
    public IReadOnlyList<EndpointDescriptor> ScanInterface(Type serviceType)
    {
        if (serviceType == null)
        {
            throw new ArgumentNullException(nameof(serviceType));
        }

        if (!serviceType.IsInterface)
        {
            throw new ArgumentException($"Type {serviceType.Name} must be an interface.", nameof(serviceType));
        }

        var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        var servicePrefix = GetServicePrefix(serviceType);
        var endpoints = new List<EndpointDescriptor>();

        foreach (var method in methods)
        {
            var endpoint = BuildEndpointDescriptor(serviceType, method, servicePrefix);
            if (endpoint != null)
            {
                endpoints.Add(endpoint);
            }
        }

        return endpoints;
    }

    private EndpointDescriptor? BuildEndpointDescriptor(Type serviceType, MethodInfo method, string servicePrefix)
    {
        // Validate return type
        var returnType = method.ReturnType;
        if (!typeof(Task).IsAssignableFrom(returnType))
        {
            return null; // Only async methods are supported
        }

        // Get the Result type
        var (resultType, resultValueType) = GetResultTypes(returnType);
        if (resultType == null)
        {
            return null; // Only methods returning Result or Result<T> are supported
        }

        var httpMethod = GetHttpMethod(method);
        var routeTemplate = BuildRouteTemplate(method, servicePrefix);
        var parameters = BuildParameterDescriptors(method, routeTemplate, httpMethod);

        return new EndpointDescriptor(
            serviceType,
            method,
            httpMethod.ToString().ToUpperInvariant(),
            routeTemplate,
            parameters,
            resultType,
            resultValueType);
    }

    private static (Type? resultType, Type? valueType) GetResultTypes(Type returnType)
    {
        // Handle Task<Result> and Task<Result<T>>
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var innerType = returnType.GetGenericArguments()[0];

            // Check for Result<T>
            if (innerType.IsGenericType)
            {
                var genericDef = innerType.GetGenericTypeDefinition();
                if (genericDef.FullName == "Voyager.Common.Results.Result`1")
                {
                    return (innerType, innerType.GetGenericArguments()[0]);
                }
            }

            // Check for Result (non-generic)
            if (innerType.FullName == "Voyager.Common.Results.Result")
            {
                return (innerType, null);
            }
        }

        return (null, null);
    }

    private static ProxyHttpMethod GetHttpMethod(MethodInfo method)
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
            name.StartsWith("List", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Search", StringComparison.OrdinalIgnoreCase))
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

    private static string GetServicePrefix(Type interfaceType)
    {
        var attribute = interfaceType.GetCustomAttribute<ServiceRouteAttribute>();
        if (attribute != null)
        {
            return "/" + attribute.Prefix.Trim('/');
        }

        // Convention: IUserService -> user-service
        var name = interfaceType.Name;
        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }

        return "/" + ToKebabCase(name);
    }

    private static string BuildRouteTemplate(MethodInfo method, string servicePrefix)
    {
        var attribute = method.GetCustomAttribute<HttpMethodAttribute>();
        var template = attribute?.Template;

        if (!string.IsNullOrEmpty(template))
        {
            // Combine with service prefix
            return CombinePaths(servicePrefix, template!);
        }

        // Convention-based: method name to kebab-case
        var methodName = method.Name;
        if (methodName.EndsWith("Async", StringComparison.Ordinal))
        {
            methodName = methodName.Substring(0, methodName.Length - 5);
        }

        return CombinePaths(servicePrefix, ToKebabCase(methodName));
    }

    private static IReadOnlyList<ParameterDescriptor> BuildParameterDescriptors(
        MethodInfo method,
        string routeTemplate,
        ProxyHttpMethod httpMethod)
    {
        var parameters = method.GetParameters();
        var routeParams = ExtractRouteParameters(routeTemplate);
        var descriptors = new List<ParameterDescriptor>();

        foreach (var param in parameters)
        {
            if (param.ParameterType == typeof(CancellationToken))
            {
                descriptors.Add(new ParameterDescriptor(
                    param.Name!,
                    param.ParameterType,
                    ParameterSource.CancellationToken,
                    true,
                    null));
                continue;
            }

            ParameterSource source;
            if (routeParams.Contains(param.Name!, StringComparer.OrdinalIgnoreCase))
            {
                source = ParameterSource.Route;
            }
            else if (IsComplexType(param.ParameterType) &&
                     (httpMethod == ProxyHttpMethod.Post ||
                      httpMethod == ProxyHttpMethod.Put ||
                      httpMethod == ProxyHttpMethod.Patch))
            {
                source = ParameterSource.Body;
            }
            else
            {
                source = ParameterSource.Query;
            }

            descriptors.Add(new ParameterDescriptor(
                param.Name!,
                param.ParameterType,
                source,
                param.HasDefaultValue,
                param.HasDefaultValue ? param.DefaultValue : null));
        }

        return descriptors;
    }

    private static HashSet<string> ExtractRouteParameters(string routeTemplate)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matches = PlaceholderRegex.Matches(routeTemplate);

        foreach (Match match in matches)
        {
            result.Add(match.Groups[1].Value);
        }

        return result;
    }

    private static bool IsComplexType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            type = underlyingType;
        }

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
