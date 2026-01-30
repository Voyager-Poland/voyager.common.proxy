namespace Voyager.Common.Proxy.Server.Swagger.Core.Models;

using System.Collections.Generic;

/// <summary>
/// Platform-agnostic representation of an OpenAPI path item.
/// </summary>
public sealed class PathDefinition
{
    /// <summary>
    /// Gets the route path (e.g., "/user-service/get-user/{id}").
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the HTTP method (GET, POST, PUT, DELETE, PATCH).
    /// </summary>
    public string HttpMethod { get; }

    /// <summary>
    /// Gets the operation definition for this path.
    /// </summary>
    public OperationDefinition Operation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PathDefinition"/> class.
    /// </summary>
    public PathDefinition(string path, string httpMethod, OperationDefinition operation)
    {
        Path = path ?? throw new System.ArgumentNullException(nameof(path));
        HttpMethod = httpMethod ?? throw new System.ArgumentNullException(nameof(httpMethod));
        Operation = operation ?? throw new System.ArgumentNullException(nameof(operation));
    }
}

/// <summary>
/// Platform-agnostic representation of an OpenAPI operation.
/// </summary>
public sealed class OperationDefinition
{
    /// <summary>
    /// Gets the unique operation ID.
    /// </summary>
    public string OperationId { get; }

    /// <summary>
    /// Gets the operation summary.
    /// </summary>
    public string? Summary { get; }

    /// <summary>
    /// Gets the operation description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets the tags for grouping operations.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Gets the parameters for this operation.
    /// </summary>
    public IReadOnlyList<ParameterDefinition> Parameters { get; }

    /// <summary>
    /// Gets the request body definition (for POST, PUT, PATCH).
    /// </summary>
    public RequestBodyDefinition? RequestBody { get; }

    /// <summary>
    /// Gets the response definitions.
    /// </summary>
    public IReadOnlyList<ResponseDefinition> Responses { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OperationDefinition"/> class.
    /// </summary>
    public OperationDefinition(
        string operationId,
        string? summary,
        string? description,
        IReadOnlyList<string> tags,
        IReadOnlyList<ParameterDefinition> parameters,
        RequestBodyDefinition? requestBody,
        IReadOnlyList<ResponseDefinition> responses)
    {
        OperationId = operationId ?? throw new System.ArgumentNullException(nameof(operationId));
        Summary = summary;
        Description = description;
        Tags = tags ?? System.Array.Empty<string>();
        Parameters = parameters ?? System.Array.Empty<ParameterDefinition>();
        RequestBody = requestBody;
        Responses = responses ?? System.Array.Empty<ResponseDefinition>();
    }
}

/// <summary>
/// Platform-agnostic representation of an OpenAPI parameter.
/// </summary>
public sealed class ParameterDefinition
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the parameter location (path, query, header).
    /// </summary>
    public ParameterLocation Location { get; }

    /// <summary>
    /// Gets the parameter description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter is required.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets the schema for the parameter value.
    /// </summary>
    public SchemaDefinition Schema { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterDefinition"/> class.
    /// </summary>
    public ParameterDefinition(
        string name,
        ParameterLocation location,
        string? description,
        bool required,
        SchemaDefinition schema)
    {
        Name = name ?? throw new System.ArgumentNullException(nameof(name));
        Location = location;
        Description = description;
        Required = required;
        Schema = schema ?? throw new System.ArgumentNullException(nameof(schema));
    }
}

/// <summary>
/// Parameter location in the request.
/// </summary>
public enum ParameterLocation
{
    /// <summary>Parameter in URL path.</summary>
    Path,
    /// <summary>Parameter in query string.</summary>
    Query,
    /// <summary>Parameter in header.</summary>
    Header
}

/// <summary>
/// Platform-agnostic representation of an OpenAPI request body.
/// </summary>
public sealed class RequestBodyDefinition
{
    /// <summary>
    /// Gets the description of the request body.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets a value indicating whether the request body is required.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets the content type (e.g., "application/json").
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Gets the schema for the request body.
    /// </summary>
    public SchemaDefinition Schema { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestBodyDefinition"/> class.
    /// </summary>
    public RequestBodyDefinition(string? description, bool required, string contentType, SchemaDefinition schema)
    {
        Description = description;
        Required = required;
        ContentType = contentType ?? "application/json";
        Schema = schema ?? throw new System.ArgumentNullException(nameof(schema));
    }
}

/// <summary>
/// Platform-agnostic representation of an OpenAPI response.
/// </summary>
public sealed class ResponseDefinition
{
    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the response description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets the content type (e.g., "application/json"). Null for no content.
    /// </summary>
    public string? ContentType { get; }

    /// <summary>
    /// Gets the schema for the response body. Null for no content.
    /// </summary>
    public SchemaDefinition? Schema { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseDefinition"/> class.
    /// </summary>
    public ResponseDefinition(int statusCode, string description, string? contentType = null, SchemaDefinition? schema = null)
    {
        StatusCode = statusCode;
        Description = description ?? throw new System.ArgumentNullException(nameof(description));
        ContentType = contentType;
        Schema = schema;
    }
}
