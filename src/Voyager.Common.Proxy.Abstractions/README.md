# Voyager.Common.Proxy.Abstractions

Zero-dependency abstractions library providing optional HTTP attributes for [Voyager.Common.Proxy](https://github.com/voyager-poland/Voyager.Common.Proxy).

## Installation

```bash
dotnet add package Voyager.Common.Proxy.Abstractions
```

## Overview

This package provides **optional** attributes for customizing HTTP routing in service interfaces. The proxy supports convention-based routing out of the box - attributes are only needed when you want to override the defaults.

## Convention-Based Routing (No Attributes Needed)

When no attributes are specified, the proxy uses these conventions:

| Method Prefix | HTTP Method | Example |
|---------------|-------------|---------|
| `Get*`, `Find*`, `List*` | GET | `GetUserAsync(int id)` → `GET /get-user?id=123` |
| `Create*`, `Add*` | POST | `CreateUserAsync(Request r)` → `POST /create-user` |
| `Update*` | PUT | `UpdateUserAsync(Request r)` → `PUT /update-user` |
| `Delete*`, `Remove*` | DELETE | `DeleteUserAsync(int id)` → `DELETE /delete-user?id=123` |
| Other | POST | `ProcessOrderAsync(Order o)` → `POST /process-order` |

```csharp
// No attributes needed - conventions handle everything
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id);
    Task<Result<User>> CreateUserAsync(CreateUserRequest request);
    Task<Result<User>> UpdateUserAsync(int id, UpdateUserRequest request);
    Task<Result> DeleteUserAsync(int id);
}
```

## Attributes for Customization

Use attributes when you need custom routes or want to override conventions.

### ServiceRouteAttribute

Sets the base route prefix for all methods in the interface.

```csharp
[ServiceRoute("api/v2/users")]
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id);
    // Results in: GET /api/v2/users/get-user?id=123
}
```

### HTTP Method Attributes

Override the HTTP method and/or route template for specific methods.

```csharp
public interface IUserService
{
    // Custom route with path parameter
    [HttpGet("users/{id}")]
    Task<Result<User>> GetUserAsync(int id);
    // Results in: GET /users/123

    // Custom route with query parameters
    [HttpGet("users")]
    Task<Result<List<User>>> SearchUsersAsync(string? name, int? limit);
    // Results in: GET /users?name=John&limit=10

    // POST with body
    [HttpPost("users")]
    Task<Result<User>> CreateUserAsync(CreateUserRequest request);
    // Results in: POST /users with JSON body

    // PUT with path parameter and body
    [HttpPut("users/{id}")]
    Task<Result<User>> UpdateUserAsync(int id, UpdateUserRequest request);
    // Results in: PUT /users/123 with JSON body

    // DELETE with path parameter
    [HttpDelete("users/{id}")]
    Task<Result> DeleteUserAsync(int id);
    // Results in: DELETE /users/123

    // PATCH for partial updates
    [HttpPatch("users/{id}")]
    Task<Result<User>> PatchUserAsync(int id, PatchUserRequest request);
    // Results in: PATCH /users/123 with JSON body
}
```

### Mixing Conventions and Attributes

You can use attributes on some methods while relying on conventions for others.

```csharp
[ServiceRoute("api/users")]
public interface IUserService
{
    // Uses attribute - custom route
    [HttpGet("{id}")]
    Task<Result<User>> GetUserAsync(int id);
    // Results in: GET /api/users/123

    // Uses convention - no attribute needed
    Task<Result<List<User>>> GetAllUsersAsync();
    // Results in: GET /api/users/get-all-users

    // Uses convention - prefix determines HTTP method
    Task<Result<User>> CreateUserAsync(CreateUserRequest request);
    // Results in: POST /api/users/create-user
}
```

## Route Template Syntax

Route templates support parameter placeholders using curly braces:

```csharp
[HttpGet("users/{userId}/orders/{orderId}")]
Task<Result<Order>> GetOrderAsync(int userId, int orderId);
// Results in: GET /users/123/orders/456
```

**Parameter matching rules:**
- Parameter names are matched **case-insensitively**
- Parameters in the template are taken from the route path
- Remaining parameters become query string parameters

```csharp
[HttpGet("users/{id}")]
Task<Result<List<Order>>> GetUserOrdersAsync(int id, string? status, int? limit);
// Results in: GET /users/123?status=pending&limit=10
```

## Available Attributes

| Attribute | Target | Description |
|-----------|--------|-------------|
| `[ServiceRoute("prefix")]` | Interface | Sets base route prefix for all methods |
| `[HttpGet("template")]` | Method | Uses HTTP GET |
| `[HttpPost("template")]` | Method | Uses HTTP POST |
| `[HttpPut("template")]` | Method | Uses HTTP PUT |
| `[HttpDelete("template")]` | Method | Uses HTTP DELETE |
| `[HttpPatch("template")]` | Method | Uses HTTP PATCH |
| `[HttpMethod(method, "template")]` | Method | Base attribute for custom scenarios |

All template parameters are optional - when omitted, routes are derived from method names.

## Supported Frameworks

- .NET Framework 4.8
- .NET 6.0
- .NET 8.0

## Related Packages

- **Voyager.Common.Proxy.Client** - HTTP client proxy generation
- **Voyager.Common.Proxy.Server** - Minimal API endpoint generation
- **Voyager.Common.Results** - Result pattern for error handling

## License

MIT License - see [LICENSE](https://github.com/voyager-poland/Voyager.Common.Proxy/blob/main/LICENSE) for details.
