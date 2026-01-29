# Voyager.Common.Proxy.Client

HTTP client proxy generation for C# interfaces using `DispatchProxy`. Automatically translates interface method calls to HTTP requests with full `Result<T>` support.

## Installation

```bash
dotnet add package Voyager.Common.Proxy.Client
```

## Quick Start

### 1. Define your service interface

```csharp
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id);
    Task<Result<User>> CreateUserAsync(CreateUserRequest request);
    Task<Result<List<User>>> GetUsersAsync(string? filter = null);
    Task<Result> DeleteUserAsync(int id);
}
```

### 2. Register with dependency injection

```csharp
// Program.cs or Startup.cs
services.AddServiceProxy<IUserService>("https://api.example.com");

// Or with options
services.AddServiceProxy<IUserService>(options =>
{
    options.BaseUrl = new Uri("https://api.example.com");
    options.Timeout = TimeSpan.FromSeconds(60);
});

// With authentication handler
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddHttpMessageHandler<AuthorizationHandler>();
```

### 3. Use the service

```csharp
public class UserController
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService; // This is the HTTP proxy
    }

    public async Task<IActionResult> GetUser(int id)
    {
        var result = await _userService.GetUserAsync(id);
        // Translates to: GET https://api.example.com/user-service/get-user?id=123

        return result.Match(
            onSuccess: user => Ok(user),
            onFailure: error => error.Type switch
            {
                ErrorType.NotFound => NotFound(error.Message),
                _ => BadRequest(error.Message)
            }
        );
    }
}
```

## HTTP Mapping

### Method Name Conventions

| Method Prefix | HTTP Method | Example |
|---------------|-------------|---------|
| `Get*`, `Find*`, `List*` | GET | `GetUserAsync(id)` → `GET /get-user?id=123` |
| `Create*`, `Add*` | POST | `CreateUserAsync(req)` → `POST /create-user` |
| `Update*` | PUT | `UpdateUserAsync(req)` → `PUT /update-user` |
| `Delete*`, `Remove*` | DELETE | `DeleteUserAsync(id)` → `DELETE /delete-user?id=123` |
| Other | POST | `ProcessAsync(data)` → `POST /process` |

### Parameter Mapping

- **Simple types** (int, string, Guid, etc.) → Query string parameters
- **Complex types** (classes, records) → JSON body (for POST, PUT, PATCH)
- **CancellationToken** → Used for request cancellation, not sent

### HTTP Status Code → Result Mapping

| HTTP Status | Result |
|-------------|--------|
| 200 OK | `Result.Success(value)` |
| 201 Created | `Result.Success(value)` |
| 204 No Content | `Result.Success()` |
| 400 Bad Request | `Result.Failure(Error.Validation(...))` |
| 401 Unauthorized | `Result.Failure(Error.Unauthorized(...))` |
| 403 Forbidden | `Result.Failure(Error.Permission(...))` |
| 404 Not Found | `Result.Failure(Error.NotFound(...))` |
| 409 Conflict | `Result.Failure(Error.Conflict(...))` |
| 408, 504 Timeout | `Result.Failure(Error.Timeout(...))` |
| 429, 503 | `Result.Failure(Error.Unavailable(...))` |
| 5xx | `Result.Failure(Error.Unexpected(...))` |

### Connection Errors

Network errors are automatically converted to `Result.Failure`:

- Connection refused → `Error.Unavailable`
- Timeout → `Error.Timeout`
- Cancellation → `Error.Cancelled`

## Custom Routes with Attributes

Use attributes from `Voyager.Common.Proxy.Abstractions` for custom routing:

```csharp
[ServiceRoute("api/v2/users")]
public interface IUserService
{
    [HttpGet("{id}")]
    Task<Result<User>> GetUserAsync(int id);
    // Results in: GET https://api.example.com/api/v2/users/123

    [HttpPost]
    Task<Result<User>> CreateUserAsync(CreateUserRequest request);
    // Results in: POST https://api.example.com/api/v2/users

    [HttpGet]
    Task<Result<List<User>>> SearchAsync(string? name, int? limit);
    // Results in: GET https://api.example.com/api/v2/users?name=John&limit=10
}
```

## Manual Proxy Creation

You can create proxies manually without DI:

```csharp
var httpClient = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
var options = new ServiceProxyOptions
{
    BaseUrl = new Uri("https://api.example.com"),
    Timeout = TimeSpan.FromSeconds(30)
};

IUserService userService = ServiceProxy<IUserService>.Create(httpClient, options);
var result = await userService.GetUserAsync(123);
```

## Authentication

`AddServiceProxy` returns `IHttpClientBuilder`, allowing you to add authentication handlers:

```csharp
// 1. Create an authorization handler
public class AuthorizationHandler : DelegatingHandler
{
    private readonly ITokenProvider _tokenProvider;

    public AuthorizationHandler(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}

// 2. Register handler and proxy
services.AddTransient<AuthorizationHandler>();
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddHttpMessageHandler<AuthorizationHandler>();
```

## Polly Integration

Add resilience policies using [Microsoft.Extensions.Http.Polly](https://www.nuget.org/packages/Microsoft.Extensions.Http.Polly):

```csharp
// Install: dotnet add package Microsoft.Extensions.Http.Polly

services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddHttpMessageHandler<AuthorizationHandler>()
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
```

## JSON Serialization

By default, the proxy uses `System.Text.Json` with camelCase naming. You can customize this:

```csharp
services.AddServiceProxy<IUserService>(options =>
{
    options.BaseUrl = new Uri("https://api.example.com");
    options.JsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
});
```

## Supported Method Signatures

All methods must:
- Return `Task<Result>` or `Task<Result<T>>`
- Be asynchronous (no synchronous methods)

```csharp
public interface IOrderService
{
    // ✅ Supported
    Task<Result<Order>> GetOrderAsync(int id);
    Task<Result<Order>> GetOrderAsync(int id, CancellationToken cancellationToken);
    Task<Result<List<Order>>> GetOrdersAsync(string? status, int? limit);
    Task<Result> DeleteOrderAsync(int id);

    // ❌ Not supported
    Result<Order> GetOrder(int id);  // Synchronous not supported
    Task<Order> GetOrderAsync(int id);  // Must return Result<T>
}
```

## Supported Frameworks

- .NET Framework 4.8 (via Castle.DynamicProxy)
- .NET 6.0 (via DispatchProxy)
- .NET 8.0 (via DispatchProxy)

## Dependencies

**Required:**
- `Voyager.Common.Proxy.Abstractions` - HTTP attributes
- `Voyager.Common.Results` - Result pattern
- `Microsoft.Extensions.Http` - HttpClientFactory

**net48 only:**
- `Castle.Core` - Dynamic proxy generation

**Optional:**
- `Microsoft.Extensions.Http.Polly` - Retry policies, circuit breakers

## Related Packages

- **Voyager.Common.Proxy.Abstractions** - HTTP attributes (optional)
- **Voyager.Common.Proxy.Server** - Server-side endpoint generation
- **Voyager.Common.Results** - Result pattern library

## License

MIT License - see [LICENSE](https://github.com/voyager-poland/Voyager.Common.Proxy/blob/main/LICENSE) for details.
