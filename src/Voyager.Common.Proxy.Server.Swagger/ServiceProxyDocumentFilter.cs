namespace Voyager.Common.Proxy.Server.Swagger;

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Voyager.Common.Proxy.Server.Swagger.Core;
using Voyager.Common.Proxy.Server.Swagger.Core.Models;

/// <summary>
/// Swashbuckle document filter that adds service proxy endpoints to the Swagger document.
/// </summary>
/// <typeparam name="TService">The service interface type.</typeparam>
public class ServiceProxyDocumentFilter<TService> : IDocumentFilter
    where TService : class
{
    /// <inheritdoc />
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var generator = new ServiceProxySwaggerGenerator();
        var paths = generator.GeneratePaths<TService>();

        foreach (var path in paths)
        {
            var openApiPath = ConvertToOpenApiPathItem(path, context.SchemaGenerator, context.SchemaRepository);

            if (swaggerDoc.Paths.ContainsKey(path.Path))
            {
                // Merge operations into existing path
                var existingPath = swaggerDoc.Paths[path.Path];
                var operationType = ParseOperationType(path.HttpMethod);
                existingPath.Operations[operationType] = openApiPath.Operations[operationType];
            }
            else
            {
                swaggerDoc.Paths.Add(path.Path, openApiPath);
            }
        }

        // Add component schemas
        foreach (var (name, schema) in generator.SchemaGenerator.ComponentSchemas)
        {
            if (!context.SchemaRepository.Schemas.ContainsKey(name))
            {
                var openApiSchema = ConvertToOpenApiSchema(schema, context.SchemaGenerator, context.SchemaRepository);
                context.SchemaRepository.Schemas.Add(name, openApiSchema);
            }
        }
    }

    private static OpenApiPathItem ConvertToOpenApiPathItem(
        PathDefinition path,
        ISchemaGenerator schemaGenerator,
        SchemaRepository schemaRepository)
    {
        var pathItem = new OpenApiPathItem();
        var operationType = ParseOperationType(path.HttpMethod);
        var operation = ConvertToOpenApiOperation(path.Operation, schemaGenerator, schemaRepository);
        pathItem.Operations[operationType] = operation;
        return pathItem;
    }

    private static OpenApiOperation ConvertToOpenApiOperation(
        OperationDefinition operation,
        ISchemaGenerator schemaGenerator,
        SchemaRepository schemaRepository)
    {
        var openApiOperation = new OpenApiOperation
        {
            OperationId = operation.OperationId,
            Summary = operation.Summary,
            Description = operation.Description,
            Tags = operation.Tags.Select(t => new OpenApiTag { Name = t }).ToList(),
            Parameters = operation.Parameters.Select(p => ConvertToOpenApiParameter(p, schemaGenerator, schemaRepository)).ToList(),
            Responses = new OpenApiResponses()
        };

        if (operation.RequestBody != null)
        {
            openApiOperation.RequestBody = ConvertToOpenApiRequestBody(operation.RequestBody, schemaGenerator, schemaRepository);
        }

        foreach (var response in operation.Responses)
        {
            openApiOperation.Responses[response.StatusCode.ToString()] = ConvertToOpenApiResponse(response, schemaGenerator, schemaRepository);
        }

        return openApiOperation;
    }

    private static OpenApiParameter ConvertToOpenApiParameter(
        ParameterDefinition parameter,
        ISchemaGenerator schemaGenerator,
        SchemaRepository schemaRepository)
    {
        return new OpenApiParameter
        {
            Name = parameter.Name,
            In = ConvertParameterLocation(parameter.Location),
            Description = parameter.Description,
            Required = parameter.Required,
            Schema = ConvertToOpenApiSchema(parameter.Schema, schemaGenerator, schemaRepository)
        };
    }

    private static OpenApiRequestBody ConvertToOpenApiRequestBody(
        RequestBodyDefinition requestBody,
        ISchemaGenerator schemaGenerator,
        SchemaRepository schemaRepository)
    {
        return new OpenApiRequestBody
        {
            Description = requestBody.Description,
            Required = requestBody.Required,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [requestBody.ContentType] = new OpenApiMediaType
                {
                    Schema = ConvertToOpenApiSchema(requestBody.Schema, schemaGenerator, schemaRepository)
                }
            }
        };
    }

    private static OpenApiResponse ConvertToOpenApiResponse(
        ResponseDefinition response,
        ISchemaGenerator schemaGenerator,
        SchemaRepository schemaRepository)
    {
        var openApiResponse = new OpenApiResponse
        {
            Description = response.Description
        };

        if (response.ContentType != null && response.Schema != null)
        {
            openApiResponse.Content = new Dictionary<string, OpenApiMediaType>
            {
                [response.ContentType] = new OpenApiMediaType
                {
                    Schema = ConvertToOpenApiSchema(response.Schema, schemaGenerator, schemaRepository)
                }
            };
        }

        return openApiResponse;
    }

    private static OpenApiSchema ConvertToOpenApiSchema(
        SchemaDefinition schema,
        ISchemaGenerator schemaGenerator,
        SchemaRepository schemaRepository)
    {
        // Handle reference
        if (schema.IsReference)
        {
            return new OpenApiSchema
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.Schema,
                    Id = schema.GetReferenceName()
                }
            };
        }

        // If we have a CLR type and it's complex, use Swashbuckle's schema generator
        if (schema.ClrType != null && schema.Type == "object" && schema.Properties == null)
        {
            return schemaGenerator.GenerateSchema(schema.ClrType, schemaRepository);
        }

        var openApiSchema = new OpenApiSchema
        {
            Type = schema.Type,
            Format = schema.Format,
            Nullable = schema.Nullable
        };

        // Handle array
        if (schema.Items != null)
        {
            openApiSchema.Items = ConvertToOpenApiSchema(schema.Items, schemaGenerator, schemaRepository);
        }

        // Handle object properties
        if (schema.Properties != null)
        {
            openApiSchema.Properties = new Dictionary<string, OpenApiSchema>();
            foreach (var (name, propSchema) in schema.Properties)
            {
                openApiSchema.Properties[name] = ConvertToOpenApiSchema(propSchema, schemaGenerator, schemaRepository);
            }

            if (schema.Required != null)
            {
                openApiSchema.Required = new HashSet<string>(schema.Required);
            }
        }

        // Handle enum
        if (schema.EnumValues != null)
        {
            openApiSchema.Enum = schema.EnumValues
                .Select(v => (Microsoft.OpenApi.Any.IOpenApiAny)new Microsoft.OpenApi.Any.OpenApiString(v))
                .ToList();
        }

        return openApiSchema;
    }

    private static Microsoft.OpenApi.Models.ParameterLocation ConvertParameterLocation(Core.Models.ParameterLocation location)
    {
        return location switch
        {
            Core.Models.ParameterLocation.Path => Microsoft.OpenApi.Models.ParameterLocation.Path,
            Core.Models.ParameterLocation.Query => Microsoft.OpenApi.Models.ParameterLocation.Query,
            Core.Models.ParameterLocation.Header => Microsoft.OpenApi.Models.ParameterLocation.Header,
            _ => Microsoft.OpenApi.Models.ParameterLocation.Query
        };
    }

    private static OperationType ParseOperationType(string httpMethod)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => OperationType.Get,
            "POST" => OperationType.Post,
            "PUT" => OperationType.Put,
            "DELETE" => OperationType.Delete,
            "PATCH" => OperationType.Patch,
            "HEAD" => OperationType.Head,
            "OPTIONS" => OperationType.Options,
            _ => OperationType.Get
        };
    }
}
