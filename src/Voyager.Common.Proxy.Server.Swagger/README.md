# Voyager.Common.Proxy.Server.Swagger

Swagger/OpenAPI integration for Voyager.Common.Proxy.Server on ASP.NET Core using Swashbuckle.

## Installation

```bash
dotnet add package Voyager.Common.Proxy.Server.Swagger
```

## Usage

### Basic Setup

```csharp
// Program.cs
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "My API", Version = "v1" });

    // Add service proxy endpoints to Swagger
    options.AddServiceProxy<IUserService>();
    options.AddServiceProxy<IOrderService>();
});

app.UseSwagger();
app.UseSwaggerUI();
```

### With Service Proxy Endpoints

```csharp
// Register services
builder.Services.AddScoped<IUserService, UserService>();

// Map service proxy endpoints
app.MapServiceProxy<IUserService>();

// Configure Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.AddServiceProxy<IUserService>();
});
```

## Features

- **Automatic Documentation**: Generates OpenAPI documentation from service interfaces
- **Result<T> Unwrapping**: Shows the actual response type, not `Result<T>`
- **Error Responses**: Automatically documents standard error responses (400, 401, 403, 404, 409, 500)
- **Parameter Binding**: Correctly identifies route, query, and body parameters
- **Schema Generation**: Full JSON Schema support for complex types

## How It Works

The `ServiceProxyDocumentFilter<T>` scans your service interface using `ServiceScanner` and generates OpenAPI path definitions:

1. Each method becomes an operation with the correct HTTP method
2. Parameters are mapped to path, query, or body based on their source
3. `Result<T>` return types are unwrapped to show just `T`
4. Standard error responses are added based on the Result pattern

## Example Output

For this interface:
```csharp
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id);
    Task<Result<User>> CreateUserAsync(CreateUserRequest request);
}
```

Swagger will show:
- `GET /user-service/get-user?id={id}` → Returns `User`
- `POST /user-service/create-user` → Request body: `CreateUserRequest`, Returns `User`
