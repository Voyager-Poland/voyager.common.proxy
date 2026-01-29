# Voyager.Common.Proxy

Interface-based HTTP client/server proxy library for .NET. Define your service contract once, use it everywhere - as an HTTP client proxy or as server endpoints.

## Overview

Voyager.Common.Proxy enables a **contract-first** approach to HTTP services. Define a C# interface with methods returning `Result<T>`, then:

- **Client-side**: Generate HTTP client proxies that translate method calls to HTTP requests
- **Server-side**: Generate HTTP endpoints from your service implementations

Both sides use the same interface contract and routing conventions, ensuring consistency across your distributed system.

```
┌─────────────────────┐                     ┌─────────────────────┐
│      Client         │                     │       Server        │
├─────────────────────┤                     ├─────────────────────┤
│                     │                     │                     │
│  IUserService       │  ───── HTTP ─────>  │  IUserService       │
│  (Proxy)            │                     │  (Implementation)   │
│                     │                     │                     │
│  GetUserAsync(1)    │  GET /get-user?id=1 │  GetUserAsync(1)    │
│  CreateUserAsync(r) │  POST /create-user  │  CreateUserAsync(r) │
│                     │                     │                     │
└─────────────────────┘                     └─────────────────────┘
```

## Features

- **Contract-first design** - Define interfaces once, use as client proxy or server endpoint
- **Convention-based routing** - HTTP methods and routes derived from method names
- **Result pattern integration** - Full support for `Voyager.Common.Results` error handling
- **Multi-framework support** - .NET 8, .NET 6, and .NET Framework 4.8
- **Zero boilerplate** - No manual HTTP client code or controller classes needed
- **Customizable** - Override conventions with attributes when needed

## Packages

| Package | Description | Targets |
|---------|-------------|---------|
| [`Voyager.Common.Proxy.Abstractions`](#abstractions) | HTTP routing attributes (optional) | net48, net6.0, net8.0 |
| [`Voyager.Common.Proxy.Client`](#client) | HTTP client proxy generation | net48, net6.0, net8.0 |
| [`Voyager.Common.Proxy.Server.AspNetCore`](#server-aspnet-core) | ASP.NET Core endpoint generation | net6.0, net8.0 |
| [`Voyager.Common.Proxy.Server.Owin`](#server-owin) | OWIN middleware for .NET Framework | net48 |

## Quick Start

### 1. Define your service interface

```csharp
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id, CancellationToken cancellationToken);
    Task<Result<User>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<Result<IEnumerable<User>>> SearchUsersAsync(string? name, int? limit, CancellationToken cancellationToken);
    Task<Result> DeleteUserAsync(int id, CancellationToken cancellationToken);
}
```

### 2. Server: Implement and expose as HTTP endpoints

```csharp
// Implement your service
public class UserService : IUserService
{
    public async Task<Result<User>> GetUserAsync(int id, CancellationToken cancellationToken)
    {
        var user = await _repository.FindAsync(id, cancellationToken);
        if (user == null)
            return Result<User>.Failure(Error.NotFoundError($"User {id} not found"));
        return Result<User>.Success(user);
    }
    // ... other methods
}

// Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();
app.MapServiceProxy<IUserService>();  // Generates HTTP endpoints
app.Run();
```

### 3. Client: Use the interface as an HTTP client

```csharp
// Register the proxy
services.AddServiceProxy<IUserService>("https://api.example.com");

// Use it like a regular service
public class UserController
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;  // This is an HTTP proxy
    }

    public async Task<IActionResult> GetUser(int id)
    {
        var result = await _userService.GetUserAsync(id, HttpContext.RequestAborted);
        // Translates to: GET https://api.example.com/user-service/get-user?id=123

        return result.Match(
            onSuccess: user => Ok(user),
            onFailure: error => NotFound(error.Message)
        );
    }
}
```

## HTTP Mapping Conventions

### Method Name to HTTP Method

| Method Prefix | HTTP Method |
|---------------|-------------|
| `Get*`, `Find*`, `List*`, `Search*` | GET |
| `Create*`, `Add*` | POST |
| `Update*` | PUT |
| `Delete*`, `Remove*` | DELETE |
| Other | POST |

### Route Generation

Routes are generated from interface and method names using kebab-case:

- Interface `IUserService` → prefix `/user-service`
- Method `GetUserAsync` → `/user-service/get-user`
- Method `CreateOrderAsync` → `/order-service/create-order`

### Parameter Binding

| Parameter Type | Location |
|----------------|----------|
| Simple types (int, string, Guid, etc.) | Query string |
| Complex types (classes, records) | Request body (POST, PUT, PATCH) |
| Route template parameters `{id}` | URL path |
| `CancellationToken` | Injected from request |

### HTTP Status to Result Mapping

| HTTP Status | Result |
|-------------|--------|
| 200 OK | `Result.Success(value)` |
| 204 No Content | `Result.Success()` |
| 400 Bad Request | `Result.Failure(Error.ValidationError(...))` |
| 401 Unauthorized | `Result.Failure(Error.UnauthorizedError(...))` |
| 404 Not Found | `Result.Failure(Error.NotFoundError(...))` |
| 5xx | `Result.Failure(Error.UnexpectedError(...))` |

## Custom Routes with Attributes

Override conventions when needed using attributes from `Voyager.Common.Proxy.Abstractions`:

```csharp
[ServiceRoute("api/v2/users")]
public interface IUserService
{
    [HttpMethod(HttpMethod.Get, "{id}")]
    Task<Result<User>> GetUserAsync(int id, CancellationToken cancellationToken);
    // GET /api/v2/users/123

    [HttpMethod(HttpMethod.Get, "")]
    Task<Result<IEnumerable<User>>> SearchAsync(string? name, int? limit, CancellationToken cancellationToken);
    // GET /api/v2/users?name=John&limit=10

    [HttpMethod(HttpMethod.Put, "{id}/status")]
    Task<Result<User>> UpdateStatusAsync(int id, UserStatus status, CancellationToken cancellationToken);
    // PUT /api/v2/users/123/status?status=Active
}
```

## Authorization

Add authorization to server endpoints using attributes or configuration:

```csharp
// Require auth for all methods in interface
[RequireAuthorization]
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id);

    [RequireAuthorization("AdminPolicy")]  // Override with specific policy
    Task<Result> DeleteUserAsync(int id);

    [AllowAnonymous]  // Public endpoint
    Task<Result<UserProfile>> GetPublicProfileAsync(int id);
}

// Or configure at mapping time
app.MapServiceProxy<IUserService>(e => e.RequireAuthorization());
```

## Package Details

### Abstractions

```bash
dotnet add package Voyager.Common.Proxy.Abstractions
```

Zero-dependency package providing optional attributes:

- `[ServiceRoute("prefix")]` - Set base route for interface
- `[HttpMethod(method, "template")]` - Override HTTP method and route
- `[RequireAuthorization]` - Require authentication
- `[RequireAuthorization("Policy")]` - Require specific policy
- `[AllowAnonymous]` - Allow anonymous access (override interface-level auth)

### Client

```bash
dotnet add package Voyager.Common.Proxy.Client
```

HTTP client proxy generation using `DispatchProxy` (.NET 6+) or `Castle.DynamicProxy` (.NET Framework 4.8):

```csharp
// Basic registration
services.AddServiceProxy<IUserService>("https://api.example.com");

// With configuration
services.AddServiceProxy<IUserService>(options =>
{
    options.BaseUrl = new Uri("https://api.example.com");
    options.Timeout = TimeSpan.FromSeconds(30);
});

// With authentication
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddHttpMessageHandler<AuthorizationHandler>();

// With Polly resilience
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddPolicyHandler(GetRetryPolicy());
```

### Server (ASP.NET Core)

```bash
dotnet add package Voyager.Common.Proxy.Server.AspNetCore
```

Minimal API endpoint generation for ASP.NET Core:

```csharp
var app = builder.Build();

// Map all methods from interface as endpoints
app.MapServiceProxy<IUserService>();
app.MapServiceProxy<IOrderService>();

app.Run();
```

### Server (OWIN)

```powershell
Install-Package Voyager.Common.Proxy.Server.Owin
```

OWIN middleware for .NET Framework 4.8:

```csharp
public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var container = new UnityContainer();
        container.RegisterType<IUserService, UserService>();

        app.Use(ServiceProxyOwinMiddleware.Create<IUserService>(
            () => container.Resolve<IUserService>()));
    }
}
```

## Supported Method Signatures

All methods must be asynchronous and return `Result` or `Result<T>`:

```csharp
public interface IOrderService
{
    // Supported
    Task<Result<Order>> GetOrderAsync(int id, CancellationToken ct);
    Task<Result<List<Order>>> GetOrdersAsync(string? status, int? limit, CancellationToken ct);
    Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct);
    Task<Result> DeleteOrderAsync(int id, CancellationToken ct);

    // NOT supported
    Result<Order> GetOrder(int id);           // Must be async
    Task<Order> GetOrderAsync(int id);        // Must return Result<T>
    Order GetOrder(int id);                   // Must be async and return Result<T>
}
```

## Dependencies

- **Voyager.Common.Results** - Result pattern for error handling (required)
- **Microsoft.Extensions.Http** - HttpClientFactory integration (client)
- **Castle.Core** - Dynamic proxy for .NET Framework 4.8 (client, net48 only)

## Project Structure

```
voyager.common.proxy/
├── src/
│   ├── Voyager.Common.Proxy.Abstractions/     # HTTP attributes
│   ├── Voyager.Common.Proxy.Client/           # Client proxy
│   ├── Voyager.Common.Proxy.Server.Abstractions/  # Server contracts
│   ├── Voyager.Common.Proxy.Server.Core/      # Server core logic
│   ├── Voyager.Common.Proxy.Server.AspNetCore/    # ASP.NET Core integration
│   └── Voyager.Common.Proxy.Server.Owin/      # OWIN integration
├── tests/
│   ├── Voyager.Common.Proxy.Client.Tests/
│   ├── Voyager.Common.Proxy.Client.IntegrationTests/
│   ├── Voyager.Common.Proxy.Server.Tests/
│   └── Voyager.Common.Proxy.Server.IntegrationTests/
├── docs/
│   └── adr/                                   # Architecture Decision Records
└── requirements/                              # Coding guidelines
```

## Building

```bash
# Restore and build
dotnet build

# Run all tests
dotnet test

# Pack NuGet packages
dotnet pack
```

## License

MIT License - Copyright (c) Sindbad IT Sp. z o.o.

See [LICENSE](https://github.com/Voyager-Poland/voyager.common.proxy/blob/main/LICENSE) for details.
