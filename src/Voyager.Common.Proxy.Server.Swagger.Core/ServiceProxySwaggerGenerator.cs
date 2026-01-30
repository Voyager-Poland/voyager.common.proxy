namespace Voyager.Common.Proxy.Server.Swagger.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using Voyager.Common.Proxy.Server.Abstractions;
using Voyager.Common.Proxy.Server.Core;
using Voyager.Common.Proxy.Server.Swagger.Core.Models;

/// <summary>
/// Generates Swagger/OpenAPI path definitions from service interfaces.
/// </summary>
public class ServiceProxySwaggerGenerator
{
    private readonly ServiceScanner _scanner;
    private readonly SchemaGenerator _schemaGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceProxySwaggerGenerator"/> class.
    /// </summary>
    public ServiceProxySwaggerGenerator()
    {
        _scanner = new ServiceScanner();
        _schemaGenerator = new SchemaGenerator();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceProxySwaggerGenerator"/> class.
    /// </summary>
    /// <param name="schemaGenerator">The schema generator to use for type schemas.</param>
    public ServiceProxySwaggerGenerator(SchemaGenerator schemaGenerator)
    {
        _scanner = new ServiceScanner();
        _schemaGenerator = schemaGenerator ?? throw new ArgumentNullException(nameof(schemaGenerator));
    }

    /// <summary>
    /// Gets the schema generator used by this instance.
    /// </summary>
    public SchemaGenerator SchemaGenerator => _schemaGenerator;

    /// <summary>
    /// Generates path definitions for the specified service interface.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <returns>A list of path definitions.</returns>
    public IReadOnlyList<PathDefinition> GeneratePaths<TService>()
        where TService : class
    {
        return GeneratePaths(typeof(TService));
    }

    /// <summary>
    /// Generates path definitions for the specified service interface.
    /// </summary>
    /// <param name="serviceType">The service interface type.</param>
    /// <returns>A list of path definitions.</returns>
    public IReadOnlyList<PathDefinition> GeneratePaths(Type serviceType)
    {
        var endpoints = _scanner.ScanInterface(serviceType);
        var paths = new List<PathDefinition>();

        foreach (var endpoint in endpoints)
        {
            var pathDefinition = CreatePathDefinition(endpoint);
            paths.Add(pathDefinition);
        }

        return paths;
    }

    private PathDefinition CreatePathDefinition(EndpointDescriptor endpoint)
    {
        var operation = CreateOperationDefinition(endpoint);
        return new PathDefinition(endpoint.RouteTemplate, endpoint.HttpMethod, operation);
    }

    private OperationDefinition CreateOperationDefinition(EndpointDescriptor endpoint)
    {
        var operationId = GetOperationId(endpoint);
        var tags = new List<string> { GetTagName(endpoint.ServiceType) };
        var parameters = CreateParameters(endpoint);
        var requestBody = CreateRequestBody(endpoint);
        var responses = CreateResponses(endpoint);

        return new OperationDefinition(
            operationId,
            summary: null,
            description: null,
            tags,
            parameters,
            requestBody,
            responses);
    }

    private static string GetOperationId(EndpointDescriptor endpoint)
    {
        var name = endpoint.Method.Name;
        if (name.EndsWith("Async", StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - 5);
        }
        return name;
    }

    private static string GetTagName(Type serviceType)
    {
        var name = serviceType.Name;
        if (name.StartsWith("I", StringComparison.Ordinal) && name.Length > 1 && char.IsUpper(name[1]))
        {
            name = name.Substring(1);
        }
        return name;
    }

    private List<ParameterDefinition> CreateParameters(EndpointDescriptor endpoint)
    {
        var parameters = new List<ParameterDefinition>();

        foreach (var param in endpoint.Parameters)
        {
            // Skip CancellationToken and Body parameters
            if (param.Source == ParameterSource.CancellationToken ||
                param.Source == ParameterSource.Body)
            {
                continue;
            }

            // Handle RouteAndQuery - expand properties as parameters
            if (param.Source == ParameterSource.RouteAndQuery)
            {
                var expandedParams = ExpandRouteAndQueryParameters(param, endpoint.RouteTemplate);
                parameters.AddRange(expandedParams);
                continue;
            }

            var location = param.Source == ParameterSource.Route
                ? ParameterLocation.Path
                : ParameterLocation.Query;

            var schema = _schemaGenerator.GenerateSchema(param.Type);

            parameters.Add(new ParameterDefinition(
                ToCamelCase(param.Name),
                location,
                description: null,
                required: param.Source == ParameterSource.Route || !param.IsOptional,
                schema));
        }

        return parameters;
    }

    private List<ParameterDefinition> ExpandRouteAndQueryParameters(
        ParameterDescriptor param,
        string routeTemplate)
    {
        var parameters = new List<ParameterDefinition>();
        var routeParams = ExtractRouteParameterNames(routeTemplate);

        var properties = param.Type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0);

        foreach (var prop in properties)
        {
            var propName = prop.Name;
            var isRouteParam = routeParams.Contains(propName, StringComparer.OrdinalIgnoreCase);
            var location = isRouteParam ? ParameterLocation.Path : ParameterLocation.Query;

            var schema = _schemaGenerator.GenerateSchema(prop.PropertyType);
            var isRequired = isRouteParam || IsPropertyRequired(prop);

            parameters.Add(new ParameterDefinition(
                ToCamelCase(propName),
                location,
                description: null,
                required: isRequired,
                schema));
        }

        return parameters;
    }

    private static HashSet<string> ExtractRouteParameterNames(string routeTemplate)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var regex = new System.Text.RegularExpressions.Regex(@"\{(\w+)(?::[^}]+)?\}");
        var matches = regex.Matches(routeTemplate);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            result.Add(match.Groups[1].Value);
        }

        return result;
    }

    private static bool IsPropertyRequired(System.Reflection.PropertyInfo property)
    {
        var propertyType = property.PropertyType;

        // Nullable types are not required
        if (Nullable.GetUnderlyingType(propertyType) != null)
        {
            return false;
        }

        // Reference types (strings, classes) are not required by default
        if (!propertyType.IsValueType)
        {
            return false;
        }

        // Non-nullable value types are required
        return true;
    }

    private RequestBodyDefinition? CreateRequestBody(EndpointDescriptor endpoint)
    {
        var bodyParam = endpoint.Parameters.FirstOrDefault(p => p.Source == ParameterSource.Body);
        if (bodyParam == null)
        {
            return null;
        }

        var schema = _schemaGenerator.GenerateSchema(bodyParam.Type);

        return new RequestBodyDefinition(
            description: null,
            required: !bodyParam.IsOptional,
            contentType: "application/json",
            schema);
    }

    private List<ResponseDefinition> CreateResponses(EndpointDescriptor endpoint)
    {
        var responses = new List<ResponseDefinition>();

        // Success response - unwrap Result<T> to T
        if (endpoint.ResultValueType != null)
        {
            var schema = _schemaGenerator.GenerateSchema(endpoint.ResultValueType);
            responses.Add(new ResponseDefinition(200, "Success", "application/json", schema));
        }
        else
        {
            responses.Add(new ResponseDefinition(204, "No Content"));
        }

        // Standard error responses based on Result error types
        responses.Add(new ResponseDefinition(400, "Validation Error", "application/json", CreateErrorSchema()));
        responses.Add(new ResponseDefinition(401, "Unauthorized"));
        responses.Add(new ResponseDefinition(403, "Forbidden"));
        responses.Add(new ResponseDefinition(404, "Not Found", "application/json", CreateErrorSchema()));
        responses.Add(new ResponseDefinition(409, "Conflict", "application/json", CreateErrorSchema()));
        responses.Add(new ResponseDefinition(500, "Server Error", "application/json", CreateErrorSchema()));

        return responses;
    }

    private static SchemaDefinition CreateErrorSchema()
    {
        return new SchemaDefinition(
            new Dictionary<string, SchemaDefinition>
            {
                ["error"] = new SchemaDefinition("string", format: null)
            },
            required: null,
            nullable: false,
            clrType: null);
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
