# Voyager.Common.Proxy.Server.AspNetCore

ASP.NET Core integration for Voyager.Common.Proxy.Server - automatically generates HTTP endpoints from service interfaces.

## Installation

```bash
dotnet add package Voyager.Common.Proxy.Server.AspNetCore
```

## Quick Start

```csharp
// Define your service interface
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id, CancellationToken cancellationToken);
    Task<Result<User>> CreateUserAsync(CreateUserRequest request);
    Task<Result<IEnumerable<User>>> SearchUsersAsync(string? name, int? limit);
    Task<Result> DeleteUserAsync(int id);
}

// Register in Program.cs
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

// Map service endpoints
app.MapServiceProxy<IUserService>();

app.Run();
```

This generates the following endpoints:

| Method | Route | Parameters |
|--------|-------|------------|
| GET | `/user-service/get-user` | `id` from query string |
| POST | `/user-service/create-user` | `request` from body |
| GET | `/user-service/search-users` | `name`, `limit` from query string |
| DELETE | `/user-service/delete-user` | `id` from query string |

## Features

- **Automatic endpoint generation** from service interfaces
- **Convention-based routing** matching `Voyager.Common.Proxy.Client`
- **Parameter binding** from route, query string, and request body
- **Result\<T\> support** - automatically unwraps successful results or returns appropriate error responses
- **CancellationToken injection** - automatically passed from `HttpContext.RequestAborted`
- **Authorization support** - attribute-based and configuration-based
- **Minimal API integration** - uses ASP.NET Core's endpoint routing

## Parameter Binding

The library automatically determines where to bind each parameter based on its type, HTTP method, and route template.

### Binding Sources

| Parameter Source | When Used | Example |
|------------------|-----------|---------|
| **Route** | Parameter name matches `{placeholder}` in route | `/users/{id}` |
| **Query** | Simple types (int, string, Guid, enum, etc.) | `?name=John&limit=10` |
| **Body** | Complex types on POST, PUT, PATCH | JSON request body |
| **Route + Query** | Complex types on GET with route placeholders | Mixed binding |
| **Injected** | `CancellationToken` | From `HttpContext.RequestAborted` |

### Simple Types from Query String

```csharp
Task<Result<List<User>>> SearchUsersAsync(string? name, int? limit, bool? active);
// GET /user-service/search-users?name=John&limit=10&active=true
```

### Route Parameters

When parameter name matches a route placeholder:

```csharp
[HttpMethod(HttpMethod.Get, "{id}")]
Task<Result<User>> GetUserAsync(int id);
// GET /user-service/123  →  id = 123
```

### Request Body (POST, PUT, PATCH)

Complex types are deserialized from JSON:

```csharp
Task<Result<User>> CreateUserAsync(CreateUserRequest request);
// POST /user-service/create-user
// Content-Type: application/json
// Body: { "name": "John", "email": "john@example.com" }
```

### Mixed Binding (Route + Query)

For GET requests with complex type parameters and route placeholders, properties are bound from both sources. **Route values take precedence** over query parameters.

```csharp
public class PaymentsListRequest
{
    public int IdBusMapCoach_RNo { get; set; }  // Bound from route
    public string? Status { get; set; }         // Bound from query
    public int? Limit { get; set; }             // Bound from query
    public DateTime? FromDate { get; set; }     // Bound from query
}

[HttpMethod(HttpMethod.Get, "payments/{IdBusMapCoach_RNo}")]
Task<Result<PaymentsList>> GetPaymentsAsync(PaymentsListRequest request);

// Request: GET /service/payments/123?Status=Active&Limit=10&FromDate=2024-01-01
//
// Binding result:
//   request.IdBusMapCoach_RNo = 123       ← from route {IdBusMapCoach_RNo}
//   request.Status = "Active"              ← from query string
//   request.Limit = 10                     ← from query string
//   request.FromDate = 2024-01-01          ← from query string
```

This enables RESTful URL design while passing multiple filter parameters:

```csharp
public class OrderFilterRequest
{
    public int CustomerId { get; set; }        // From route
    public string? Status { get; set; }        // From query
    public DateTime? Since { get; set; }       // From query
    public int Page { get; set; } = 1;         // From query (with default)
    public int PageSize { get; set; } = 20;    // From query (with default)
}

[HttpMethod(HttpMethod.Get, "customers/{CustomerId}/orders")]
Task<Result<PagedList<Order>>> GetCustomerOrdersAsync(OrderFilterRequest filter);

// GET /service/customers/42/orders?Status=Pending&Page=2&PageSize=50
```

### Supported Types for Route/Query Binding

| Type | Example Values |
|------|----------------|
| `int`, `long`, `short`, `byte` | `123`, `-456` |
| `float`, `double`, `decimal` | `19.99`, `3.14159` |
| `bool` | `true`, `false` |
| `string` | `hello`, `John%20Doe` (URL encoded) |
| `Guid` | `550e8400-e29b-41d4-a716-446655440000` |
| `DateTime` | `2024-01-15`, `2024-01-15T10:30:00` |
| `DateTimeOffset` | `2024-01-15T10:30:00+02:00` |
| `TimeSpan` | `01:30:00`, `1.12:00:00` |
| Enums | `Active`, `PENDING` (case-insensitive) |
| Nullable versions | `int?`, `bool?`, `DateTime?`, etc. |

## Authorization

### Option 1: Attribute on Interface (all endpoints)

```csharp
using Voyager.Common.Proxy.Abstractions;

[RequireAuthorization]
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id);
    Task<Result<User>> CreateUserAsync(CreateUserRequest request);
}
```

### Option 2: Attribute on Methods (specific endpoints)

```csharp
public interface IProductService
{
    // Public endpoint
    Task<Result<Product>> GetProductAsync(int id);

    // Requires authentication
    [RequireAuthorization]
    Task<Result<Product>> CreateProductAsync(CreateProductRequest request);

    // Requires specific policy
    [RequireAuthorization("AdminPolicy")]
    Task<Result> DeleteProductAsync(int id);
}
```

### Option 3: Mixed (Interface + AllowAnonymous)

```csharp
[RequireAuthorization]
public interface IOrderService
{
    // Requires authentication (inherited)
    Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request);

    // Public endpoint (override)
    [AllowAnonymous]
    Task<Result<OrderStatus>> GetOrderStatusAsync(int id);

    // Requires admin role
    [RequireAuthorization("AdminPolicy")]
    Task<Result> CancelOrderAsync(int id);
}
```

### Option 4: Configuration at Mapping

```csharp
// Apply to all endpoints in service
app.MapServiceProxy<IUserService>(e => e.RequireAuthorization());

// Apply specific policy
app.MapServiceProxy<IAdminService>(e => e.RequireAuthorization("AdminPolicy"));

// Combine with other conventions
app.MapServiceProxy<IOrderService>(e => e
    .RequireAuthorization()
    .RequireCors("AllowAll"));
```

### Authorization Attributes

| Attribute | Target | Description |
|-----------|--------|-------------|
| `[RequireAuthorization]` | Interface, Method | Requires authenticated user |
| `[RequireAuthorization("Policy")]` | Interface, Method | Requires specific policy |
| `[AllowAnonymous]` | Method | Allows anonymous access (overrides interface) |

You can also use ASP.NET Core's `[Authorize]` attribute - both are supported.

### Setting up Authorization

```csharp
var builder = WebApplication.CreateBuilder(args);

// Add authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* ... */ });

// Add authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("Admin"));
});

builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapServiceProxy<IUserService>();

app.Run();
```

## Service Resolution

Services are resolved from `IServiceProvider` for each request:

```csharp
// With service provider (default)
app.MapServiceProxy<IUserService>();
```

## Custom Permission Checking

For fine-grained access control beyond role-based authorization, use the permission checker:

### Inline Permission Checker

```csharp
app.MapServiceProxy<IVIPService>(options =>
{
    options.PermissionChecker = async ctx =>
    {
        // Check authentication
        if (ctx.User?.Identity?.IsAuthenticated != true)
            return PermissionResult.Unauthenticated();

        // Access HttpContext for services
        var httpContext = (HttpContext)ctx.RawContext;
        var checker = httpContext.RequestServices.GetRequiredService<IVIPPermissionChecker>();

        // Check permission based on method and parameters
        return await checker.CheckAsync(
            ctx.User,
            ctx.Method.Name,
            ctx.Parameters);
    };
});
```

### Permission Checker with Context-Aware Factory

```csharp
app.MapServiceProxy<IVIPService>(options =>
{
    // Create service with per-request identity
    options.ContextAwareFactory = httpContext =>
    {
        var identity = PilotIdentityFactory.Create(httpContext.User);
        var actionModule = httpContext.RequestServices.GetRequiredService<ActionModule>();
        return new VIPService(identity, actionModule);
    };

    // Check permissions before service creation
    options.PermissionChecker = async ctx =>
    {
        if (ctx.User?.Identity?.IsAuthenticated != true)
            return PermissionResult.Unauthenticated();

        // Method-level checks
        if (ctx.Method.Name == "DeleteAsync" && !ctx.User.IsInRole("Admin"))
            return PermissionResult.Denied("Admin role required for delete operations");

        // Parameter-level checks
        if (ctx.Parameters.TryGetValue("id", out var idObj) && idObj is int id)
        {
            var httpContext = (HttpContext)ctx.RawContext;
            var ownershipChecker = httpContext.RequestServices.GetRequiredService<IOwnershipChecker>();

            if (!await ownershipChecker.CanAccessAsync(ctx.User, id))
                return PermissionResult.Denied("You don't have access to this resource");
        }

        return PermissionResult.Granted();
    };
});
```

### Typed Permission Checker (Reusable)

For complex permission logic, implement `IServicePermissionChecker<TService>`:

```csharp
public class VIPServicePermissionChecker : IServicePermissionChecker<IVIPService>
{
    private readonly IOwnershipService _ownershipService;

    public VIPServicePermissionChecker(IOwnershipService ownershipService)
    {
        _ownershipService = ownershipService;
    }

    public async Task<PermissionResult> CheckPermissionAsync(PermissionContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return PermissionResult.Unauthenticated();

        // Different rules for different methods
        return context.Method.Name switch
        {
            "GetAsync" => PermissionResult.Granted(),
            "CreateAsync" => CheckCreatePermission(context),
            "DeleteAsync" => await CheckDeletePermissionAsync(context),
            _ => PermissionResult.Granted()
        };
    }

    private PermissionResult CheckCreatePermission(PermissionContext context)
    {
        if (!context.User!.IsInRole("Creator"))
            return PermissionResult.Denied("Creator role required");
        return PermissionResult.Granted();
    }

    private async Task<PermissionResult> CheckDeletePermissionAsync(PermissionContext context)
    {
        if (context.Parameters.TryGetValue("id", out var idObj) && idObj is int id)
        {
            var canDelete = await _ownershipService.CanDeleteAsync(context.User!, id);
            if (!canDelete)
                return PermissionResult.Denied("Cannot delete this resource");
        }
        return PermissionResult.Granted();
    }
}

// Registration
builder.Services.AddScoped<VIPServicePermissionChecker>();

app.MapServiceProxy<IVIPService>(options =>
{
    options.PermissionCheckerInstance = app.Services
        .GetRequiredService<VIPServicePermissionChecker>();
});
```

### PermissionContext Properties

| Property | Type | Description |
|----------|------|-------------|
| `User` | `IPrincipal?` | The authenticated user (null for anonymous) |
| `ServiceType` | `Type` | The service interface type |
| `Method` | `MethodInfo` | The method being called |
| `Endpoint` | `EndpointDescriptor` | Route and parameter info |
| `Parameters` | `IReadOnlyDictionary<string, object?>` | Deserialized request parameters |
| `RawContext` | `object` | The `HttpContext` (cast to access) |

### PermissionResult Factory Methods

| Method | HTTP Status | Use Case |
|--------|-------------|----------|
| `PermissionResult.Granted()` | - | Allow access |
| `PermissionResult.Denied(reason)` | 403 Forbidden | User authenticated but not allowed |
| `PermissionResult.Unauthenticated()` | 401 Unauthorized | User not authenticated |

## Target Frameworks

- `net6.0`
- `net8.0`
