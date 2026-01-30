namespace Voyager.Common.Proxy.Server.Swagger.Owin;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http.Description;
using global::Swagger.Net;
using Voyager.Common.Proxy.Server.Swagger.Core;
using Voyager.Common.Proxy.Server.Swagger.Core.Models;

/// <summary>
/// Swagger.Net document filter that adds service proxy endpoints to the Swagger document.
/// </summary>
/// <typeparam name="TService">The service interface type.</typeparam>
public class ServiceProxyDocumentFilter<TService> : IDocumentFilter
    where TService : class
{
    /// <inheritdoc />
    public void Apply(SwaggerDocument swaggerDoc, SchemaRegistry schemaRegistry, IApiExplorer apiExplorer)
    {
        var generator = new ServiceProxySwaggerGenerator();
        var paths = generator.GeneratePaths<TService>();

        if (swaggerDoc.paths == null)
        {
            swaggerDoc.paths = new Dictionary<string, PathItem>();
        }

        foreach (var path in paths)
        {
            var swaggerPath = ConvertToSwaggerPathItem(path, schemaRegistry);

            if (swaggerDoc.paths.ContainsKey(path.Path))
            {
                // Merge operations into existing path
                MergePathItem(swaggerDoc.paths[path.Path], swaggerPath, path.HttpMethod);
            }
            else
            {
                swaggerDoc.paths.Add(path.Path, swaggerPath);
            }
        }

        // Add component schemas to definitions
        if (swaggerDoc.definitions == null)
        {
            swaggerDoc.definitions = new Dictionary<string, Schema>();
        }

        foreach (var kvp in generator.SchemaGenerator.ComponentSchemas)
        {
            if (!swaggerDoc.definitions.ContainsKey(kvp.Key))
            {
                var swaggerSchema = ConvertToSwaggerSchema(kvp.Value, schemaRegistry);
                swaggerDoc.definitions.Add(kvp.Key, swaggerSchema);
            }
        }
    }

    private static PathItem ConvertToSwaggerPathItem(PathDefinition path, SchemaRegistry schemaRegistry)
    {
        var pathItem = new PathItem();
        var operation = ConvertToSwaggerOperation(path.Operation, schemaRegistry);

        switch (path.HttpMethod.ToUpperInvariant())
        {
            case "GET":
                pathItem.get = operation;
                break;
            case "POST":
                pathItem.post = operation;
                break;
            case "PUT":
                pathItem.put = operation;
                break;
            case "DELETE":
                pathItem.delete = operation;
                break;
            case "PATCH":
                pathItem.patch = operation;
                break;
            case "HEAD":
                pathItem.head = operation;
                break;
            case "OPTIONS":
                pathItem.options = operation;
                break;
        }

        return pathItem;
    }

    private static void MergePathItem(PathItem existing, PathItem newPath, string httpMethod)
    {
        switch (httpMethod.ToUpperInvariant())
        {
            case "GET":
                existing.get = newPath.get ?? existing.get;
                break;
            case "POST":
                existing.post = newPath.post ?? existing.post;
                break;
            case "PUT":
                existing.put = newPath.put ?? existing.put;
                break;
            case "DELETE":
                existing.delete = newPath.delete ?? existing.delete;
                break;
            case "PATCH":
                existing.patch = newPath.patch ?? existing.patch;
                break;
            case "HEAD":
                existing.head = newPath.head ?? existing.head;
                break;
            case "OPTIONS":
                existing.options = newPath.options ?? existing.options;
                break;
        }
    }

    private static Operation ConvertToSwaggerOperation(OperationDefinition operation, SchemaRegistry schemaRegistry)
    {
        var swaggerOperation = new Operation
        {
            operationId = operation.OperationId,
            summary = operation.Summary,
            description = operation.Description,
            tags = operation.Tags.ToList(),
            produces = new List<string> { "application/json" },
            consumes = new List<string> { "application/json" },
            parameters = operation.Parameters.Select(p => ConvertToSwaggerParameter(p, schemaRegistry)).ToList(),
            responses = new Dictionary<string, Response>()
        };

        if (operation.RequestBody != null)
        {
            var bodyParam = ConvertRequestBodyToParameter(operation.RequestBody, schemaRegistry);
            swaggerOperation.parameters.Add(bodyParam);
        }

        foreach (var response in operation.Responses)
        {
            swaggerOperation.responses[response.StatusCode.ToString()] = ConvertToSwaggerResponse(response, schemaRegistry);
        }

        return swaggerOperation;
    }

    private static Parameter ConvertToSwaggerParameter(ParameterDefinition parameter, SchemaRegistry schemaRegistry)
    {
        var swaggerParam = new Parameter
        {
            name = parameter.Name,
            @in = ConvertParameterLocation(parameter.Location),
            description = parameter.Description,
            required = parameter.Required
        };

        // For path and query parameters, set type directly
        if (parameter.Location != Core.Models.ParameterLocation.Header)
        {
            SetParameterType(swaggerParam, parameter.Schema);
        }

        return swaggerParam;
    }

    private static Parameter ConvertRequestBodyToParameter(RequestBodyDefinition requestBody, SchemaRegistry schemaRegistry)
    {
        return new Parameter
        {
            name = "body",
            @in = "body",
            description = requestBody.Description,
            required = requestBody.Required,
            schema = ConvertToSwaggerSchema(requestBody.Schema, schemaRegistry)
        };
    }

    private static void SetParameterType(Parameter param, SchemaDefinition schema)
    {
        if (schema.IsReference)
        {
            param.type = "string"; // Fallback for complex types in query/path
            return;
        }

        param.type = schema.Type;
        param.format = schema.Format;

        if (schema.EnumValues != null)
        {
            param.@enum = schema.EnumValues.Cast<object>().ToList();
        }
    }

    private static Response ConvertToSwaggerResponse(ResponseDefinition response, SchemaRegistry schemaRegistry)
    {
        var swaggerResponse = new Response
        {
            description = response.Description
        };

        if (response.Schema != null)
        {
            swaggerResponse.schema = ConvertToSwaggerSchema(response.Schema, schemaRegistry);
        }

        return swaggerResponse;
    }

    private static Schema ConvertToSwaggerSchema(SchemaDefinition schema, SchemaRegistry schemaRegistry)
    {
        // Handle reference
        if (schema.IsReference)
        {
            return new Schema
            {
                @ref = schema.Reference
            };
        }

        // If we have a CLR type and it's complex, try to use SchemaRegistry
        if (schema.ClrType != null && !IsPrimitiveType(schema.ClrType))
        {
            try
            {
                return schemaRegistry.GetOrRegister(schema.ClrType);
            }
            catch
            {
                // Fallback to manual schema generation
            }
        }

        var swaggerSchema = new Schema
        {
            type = schema.Type,
            format = schema.Format
        };

        // Handle array
        if (schema.Items != null)
        {
            swaggerSchema.items = ConvertToSwaggerSchema(schema.Items, schemaRegistry);
        }

        // Handle object properties
        if (schema.Properties != null)
        {
            swaggerSchema.properties = new Dictionary<string, Schema>();
            foreach (var kvp in schema.Properties)
            {
                swaggerSchema.properties[kvp.Key] = ConvertToSwaggerSchema(kvp.Value, schemaRegistry);
            }

            if (schema.Required != null)
            {
                swaggerSchema.required = schema.Required.ToList();
            }
        }

        // Handle enum
        if (schema.EnumValues != null)
        {
            swaggerSchema.@enum = schema.EnumValues.Cast<object>().ToList();
        }

        return swaggerSchema;
    }

    private static bool IsPrimitiveType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            type = underlyingType;
        }

        return type.IsPrimitive ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type.IsEnum;
    }

    private static string ConvertParameterLocation(Core.Models.ParameterLocation location)
    {
        return location switch
        {
            Core.Models.ParameterLocation.Path => "path",
            Core.Models.ParameterLocation.Query => "query",
            Core.Models.ParameterLocation.Header => "header",
            _ => "query"
        };
    }
}
