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

## Target Frameworks

- `net6.0`
- `net8.0`
