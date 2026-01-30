# Voyager.Common.Proxy.Server.Swagger.Owin

Swagger/OpenAPI integration for Voyager.Common.Proxy.Server on OWIN/.NET Framework 4.8 using Swagger-Net.

## Installation

```
Install-Package Voyager.Common.Proxy.Server.Swagger.Owin
```

## Usage

### Basic Setup

In your `SwaggerConfig.cs`:

```csharp
using Voyager.Common.Proxy.Server.Swagger.Owin;

public class SwaggerConfig
{
    public static void Register()
    {
        GlobalConfiguration.Configuration
            .EnableSwagger(c =>
            {
                c.SingleApiVersion("v1", "My API");

                // Add service proxy endpoints to Swagger
                c.AddServiceProxy<IUserService>();
                c.AddServiceProxy<IOrderService>();
            })
            .EnableSwaggerUi();
    }
}
```

### With Service Proxy Middleware

```csharp
// In Startup.cs
public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var container = new UnityContainer();
        container.RegisterType<IUserService, UserService>();

        // Add service proxy middleware
        app.Use(ServiceProxyOwinMiddleware.Create<IUserService>(
            () => container.Resolve<IUserService>()));

        // Configure Web API and Swagger
        var config = new HttpConfiguration();
        SwaggerConfig.Register();
        app.UseWebApi(config);
    }
}
```

## Features

- **Automatic Documentation**: Generates Swagger 2.0 documentation from service interfaces
- **Result<T> Unwrapping**: Shows the actual response type, not `Result<T>`
- **Error Responses**: Automatically documents standard error responses (400, 401, 403, 404, 409, 500)
- **Parameter Binding**: Correctly identifies route, query, and body parameters
- **Schema Generation**: Full JSON Schema support for complex types

## How It Works

The `ServiceProxyDocumentFilter<T>` scans your service interface using `ServiceScanner` and generates Swagger path definitions:

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
- `GET /user-service/get-user?id={id}` -> Returns `User`
- `POST /user-service/create-user` -> Request body: `CreateUserRequest`, Returns `User`

## Compatibility

- .NET Framework 4.8
- Swagger-Net 8.5.x
- OWIN/Katana
