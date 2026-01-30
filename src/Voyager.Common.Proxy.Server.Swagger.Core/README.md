# Voyager.Common.Proxy.Server.Swagger.Core

Core library for generating Swagger/OpenAPI documentation from Voyager.Common.Proxy service interfaces.

## Overview

This package provides the shared logic for Swagger generation, used by both:
- `Voyager.Common.Proxy.Server.Swagger` (ASP.NET Core / Swashbuckle)
- `Voyager.Common.Proxy.Server.Swagger.Owin` (OWIN / Swagger.Net)

## Key Components

### ServiceProxySwaggerGenerator

Generates platform-agnostic path definitions from service interfaces:

```csharp
var generator = new ServiceProxySwaggerGenerator();
var paths = generator.GeneratePaths<IUserService>();

// Access generated component schemas
var schemas = generator.SchemaGenerator.ComponentSchemas;
```

### SchemaGenerator

Generates JSON Schema definitions from .NET types:

```csharp
var schemaGenerator = new SchemaGenerator();
var schema = schemaGenerator.GenerateSchema(typeof(User));

// Get all generated component schemas
var componentSchemas = schemaGenerator.ComponentSchemas;
```

## Features

- **Result<T> Unwrapping**: Automatically extracts `T` from `Result<T>` for response schemas
- **Parameter Binding**: Correctly identifies route, query, and body parameters
- **Error Responses**: Automatically adds standard error responses (400, 401, 403, 404, 409, 500)
- **Schema Generation**: Generates JSON Schema for complex types, arrays, enums, and primitives
- **Nullable Support**: Correctly handles nullable types and reference types

## Models

- `PathDefinition` - Represents an API path with HTTP method and operation
- `OperationDefinition` - Represents an API operation with parameters, request body, and responses
- `ParameterDefinition` - Represents a path, query, or header parameter
- `RequestBodyDefinition` - Represents a request body with content type and schema
- `ResponseDefinition` - Represents an HTTP response with status code and schema
- `SchemaDefinition` - Platform-agnostic JSON Schema representation
