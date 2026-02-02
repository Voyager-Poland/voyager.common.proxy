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
| [`Voyager.Common.Proxy.Diagnostics`](#diagnostics) | Logging diagnostics handler | net48, net6.0, net8.0 |
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

The library automatically determines where to bind each parameter based on its type, HTTP method, and route template.

#### Binding Rules

| Scenario | Source | Example |
|----------|--------|---------|
| Simple type (int, string, Guid, bool, enum, etc.) | Query string | `?id=123` |
| Simple type matching route placeholder | URL path | `/users/{id}` → `/users/123` |
| Complex type on POST, PUT, PATCH | Request body (JSON) | `{ "name": "John" }` |
| Complex type on GET with route placeholders | Route + Query | See below |
| `CancellationToken` | Injected from `HttpContext.RequestAborted` | - |

#### Simple Types from Query String

```csharp
Task<Result<List<User>>> SearchUsersAsync(string? name, int? limit, bool? active);
// GET /user-service/search-users?name=John&limit=10&active=true
```

#### Simple Types from Route

When parameter name matches a route placeholder, it binds from the URL path:

```csharp
[HttpMethod(HttpMethod.Get, "{id}")]
Task<Result<User>> GetUserAsync(int id);
// GET /user-service/123
```

#### Complex Types from Request Body

For POST, PUT, PATCH methods, complex types are deserialized from JSON body:

```csharp
Task<Result<User>> CreateUserAsync(CreateUserRequest request);
// POST /user-service/create-user
// Body: { "name": "John", "email": "john@example.com" }
```

#### Complex Types from Route + Query (Mixed Binding)

When you have a GET method with route placeholders and a complex type parameter, properties are bound from both route values and query string. **Route values take precedence** over query parameters.

```csharp
public class PaymentsListRequest
{
    public int IdBusMapCoach_RNo { get; set; }  // From route
    public string? Status { get; set; }         // From query
    public int? Limit { get; set; }             // From query
}

[HttpMethod(HttpMethod.Get, "payments/{IdBusMapCoach_RNo}")]
Task<Result<PaymentsList>> GetPaymentsAsync(PaymentsListRequest request);
// GET /service/payments/123?Status=Active&Limit=10
//
// Result:
//   request.IdBusMapCoach_RNo = 123    (from route)
//   request.Status = "Active"           (from query)
//   request.Limit = 10                  (from query)
```

This is useful when you need to pass multiple parameters to a GET endpoint while keeping some in the URL path for RESTful design.

#### Supported Simple Types

The following types can be bound from route or query string:

- Primitive types: `int`, `long`, `short`, `byte`, `float`, `double`, `decimal`, `bool`
- Nullable primitives: `int?`, `long?`, `bool?`, etc.
- `string`
- `Guid`
- `DateTime`, `DateTimeOffset`, `TimeSpan`
- Enums (case-insensitive matching)

### HTTP Status to Result Mapping

| HTTP Status | Result | Transient? |
|-------------|--------|------------|
| 200 OK | `Result.Success(value)` | - |
| 204 No Content | `Result.Success()` | - |
| 400 Bad Request | `Error.ValidationError(...)` | No |
| 401 Unauthorized | `Error.UnauthorizedError(...)` | No |
| 403 Forbidden | `Error.PermissionError(...)` | No |
| 404 Not Found | `Error.NotFoundError(...)` | No |
| 408 Request Timeout | `Error.TimeoutError(...)` | Yes |
| 409 Conflict | `Error.ConflictError(...)` | No |
| 429 Too Many Requests | `Error.TooManyRequestsError(...)` | Yes |
| 502 Bad Gateway | `Error.UnavailableError(...)` | Yes |
| 503 Service Unavailable | `Error.UnavailableError(...)` | Yes |
| 504 Gateway Timeout | `Error.TimeoutError(...)` | Yes |
| 500, other 5xx | `Error.UnexpectedError(...)` | No |

Transient errors can be retried with exponential backoff. Use `error.Type.IsTransient()` to check.

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

// Custom permission checker for fine-grained control
app.MapServiceProxy<IVIPService>(options =>
{
    options.PermissionChecker = async ctx =>
    {
        if (ctx.User?.Identity?.IsAuthenticated != true)
            return PermissionResult.Unauthenticated();

        if (ctx.Method.Name == "DeleteAsync" && !ctx.User.IsInRole("Admin"))
            return PermissionResult.Denied("Admin role required");

        return PermissionResult.Granted();
    };
});
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

### Diagnostics

```bash
dotnet add package Voyager.Common.Proxy.Diagnostics
```

Logging and observability for proxy operations:

```csharp
// Add logging diagnostics
services.AddLogging(b => b.AddConsole());
services.AddProxyLoggingDiagnostics();

// Add user context for request tracking
services.AddHttpContextAccessor();
services.AddProxyRequestContext<HttpContextRequestContext>();

// Custom diagnostics handler
public class MetricsDiagnostics : ProxyDiagnosticsHandler
{
    public override void OnRequestCompleted(RequestCompletedEvent e)
    {
        _metrics.RecordDuration(e.ServiceName, e.MethodName, e.Duration);
    }

    public override void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
    {
        if (e.NewState == "Open")
            _alerting.SendAlert($"Circuit breaker opened: {e.ServiceName}");
    }
}

services.AddProxyDiagnostics<MetricsDiagnostics>();
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

- **Voyager.Common.Results** (>= 1.7.0) - Result pattern and error classification
- **Voyager.Common.Resilience** (>= 1.7.0) - Circuit breaker for client proxies
- **Microsoft.Extensions.Http** - HttpClientFactory integration (client)
- **Castle.Core** - Dynamic proxy for .NET Framework 4.8 (client, net48 only)

## Project Structure

```
voyager.common.proxy/
├── src/
│   ├── Voyager.Common.Proxy.Abstractions/     # HTTP attributes, diagnostic interfaces
│   ├── Voyager.Common.Proxy.Client/           # Client proxy
│   ├── Voyager.Common.Proxy.Diagnostics/      # Logging diagnostics handler
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
