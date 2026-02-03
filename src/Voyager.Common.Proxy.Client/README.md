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
- **Complex types** (classes, records):
  - For **POST, PUT, PATCH** → JSON body
  - For **GET, DELETE** → Properties extracted as query string parameters
- **CancellationToken** → Used for request cancellation, not sent

### Complex Types in GET/DELETE Requests

For GET and DELETE methods, complex type parameters are automatically decomposed into query string parameters:

```csharp
public class SearchQuery
{
    public string? Name { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public interface IUserService
{
    Task<Result<List<User>>> GetUsersAsync(SearchQuery query);
    // GetUsersAsync(new SearchQuery { Name = "john", Page = 1, PageSize = 10 })
    // → GET /user-service/get-users?Name=john&Page=1&PageSize=10
}
```

Properties can also be used in route templates:

```csharp
public class UserOrdersQuery
{
    public int UserId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; }
}

public interface IOrderService
{
    [HttpGet("users/{UserId}/orders")]
    Task<Result<List<Order>>> GetUserOrdersAsync(UserOrdersQuery query);
    // GetUserOrdersAsync(new UserOrdersQuery { UserId = 123, Status = "pending", Page = 1 })
    // → GET /order-service/users/123/orders?Status=pending&Page=1
}
```

**Rules:**
- Null properties are omitted from query string
- Properties used in route template are not duplicated in query string
- Nested complex types are skipped (only simple types are extracted)
- For POST/PUT/PATCH, complex types still become JSON body

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
| 429, 502, 503 | `Result.Failure(Error.Unavailable(...))` |
| 500 | `Result.Failure(Error.Unexpected(...))` |
| Other 5xx | `Result.Failure(Error.Unexpected(...))` |

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

`AddServiceProxy` returns `IHttpClientBuilder`, allowing you to add authentication handlers via `AddHttpMessageHandler<T>()`.

### ASP.NET Core - Forward Current User's Token

Forward the Bearer token from incoming request to outgoing service calls:

```csharp
public class ForwardAuthorizationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ForwardAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var authHeader = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();

        if (!string.IsNullOrEmpty(authHeader))
        {
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

// Registration in Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ForwardAuthorizationHandler>();
builder.Services.AddServiceProxy<IPaymentService>("https://api.internal.com")
    .AddHttpMessageHandler<ForwardAuthorizationHandler>();
```

### ASP.NET Core - Custom Token Provider

Use a token provider for service-to-service authentication (client credentials):

```csharp
public class ServiceAuthorizationHandler : DelegatingHandler
{
    private readonly ITokenProvider _tokenProvider;

    public ServiceAuthorizationHandler(ITokenProvider tokenProvider)
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

// Registration
services.AddSingleton<ITokenProvider, ClientCredentialsTokenProvider>();
services.AddTransient<ServiceAuthorizationHandler>();
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddHttpMessageHandler<ServiceAuthorizationHandler>();
```

### OWIN / .NET Framework 4.8

In OWIN applications, use `HttpContext.Current` to access the incoming request:

```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

public class OwinForwardAuthorizationHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Forward Authorization header from incoming request
        var incomingAuth = HttpContext.Current?.Request?.Headers["Authorization"];

        if (!string.IsNullOrEmpty(incomingAuth))
        {
            request.Headers.TryAddWithoutValidation("Authorization", incomingAuth);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

// Registration in Startup.cs
public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var services = new ServiceCollection();

        services.AddTransient<OwinForwardAuthorizationHandler>();
        services.AddServiceProxy<IPaymentService>("https://api.internal.com")
            .AddHttpMessageHandler<OwinForwardAuthorizationHandler>();

        // Build and use ServiceProvider...
    }
}
```

### OWIN - Token from Claims

Extract token stored in user claims:

```csharp
public class OwinClaimsAuthorizationHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Get token from OWIN context
        var owinContext = HttpContext.Current?.GetOwinContext();
        var claimsPrincipal = owinContext?.Authentication?.User;

        // Token stored in claims (e.g., during OAuth authentication)
        var token = claimsPrincipal?.FindFirst("access_token")?.Value
                 ?? claimsPrincipal?.FindFirst("id_token")?.Value;

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

### OWIN - Custom Token Provider with Caching

For service-to-service calls with token caching:

```csharp
public class OwinServiceAuthorizationHandler : DelegatingHandler
{
    private readonly ITokenProvider _tokenProvider;
    private string _cachedToken;
    private DateTime _tokenExpiry;

    public OwinServiceAuthorizationHandler(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_cachedToken) || DateTime.UtcNow >= _tokenExpiry)
        {
            var tokenResult = await _tokenProvider.GetTokenAsync(cancellationToken);
            _cachedToken = tokenResult.Token;
            _tokenExpiry = tokenResult.ExpiresAt.AddMinutes(-5); // Refresh 5 min early
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _cachedToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
```

### Multiple Handlers

Chain multiple handlers for complex scenarios:

```csharp
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddHttpMessageHandler<LoggingHandler>()           // 1. Log request/response
    .AddHttpMessageHandler<ForwardAuthorizationHandler>() // 2. Add auth header
    .AddHttpMessageHandler<CorrelationIdHandler>();    // 3. Add correlation ID
```

### Alternative: DelegatingHandlerFactories (Unity/DI Bridging)

In some scenarios, `AddHttpMessageHandler<T>()` may not work correctly—particularly when using DI container bridges (e.g., Unity adapter for `Microsoft.Extensions.DependencyInjection`). The handlers may not be resolved or the pipeline may not be constructed properly.

For these cases, use `DelegatingHandlerFactories` which builds the HTTP pipeline manually:

```csharp
// ASP.NET Core or OWIN - works reliably with Unity and other DI bridges
services.AddServiceProxy<IPaymentService>(options =>
{
    options.BaseUrl = new Uri("https://api.internal.com");

    // Add handlers via factories - bypasses IHttpClientFactory.AddHttpMessageHandler
    options.DelegatingHandlerFactories.Add(sp =>
        sp.GetRequiredService<LoggingHandler>());
    options.DelegatingHandlerFactories.Add(sp =>
        sp.GetRequiredService<ForwardAuthorizationHandler>());
});

// Don't forget to register the handlers
services.AddTransient<LoggingHandler>();
services.AddTransient<ForwardAuthorizationHandler>();
```

**OWIN / .NET Framework 4.8 with Unity:**

```csharp
public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var services = new ServiceCollection();

        // Register handlers
        services.AddTransient<OwinForwardAuthorizationHandler>();
        services.AddTransient<LoggingHandler>();

        // Use DelegatingHandlerFactories instead of AddHttpMessageHandler
        services.AddServiceProxy<IPaymentService>(options =>
        {
            options.BaseUrl = new Uri("https://api.internal.com");
            options.DelegatingHandlerFactories.Add(sp =>
                sp.GetRequiredService<LoggingHandler>());
            options.DelegatingHandlerFactories.Add(sp =>
                sp.GetRequiredService<OwinForwardAuthorizationHandler>());
        });

        // Bridge to Unity container
        var serviceProvider = services.BuildServiceProvider();
        // ... configure Unity adapter
    }
}
```

**When to use DelegatingHandlerFactories:**
- Unity container bridging scenarios
- Other DI container adapters where `AddHttpMessageHandler` doesn't work
- When you need full control over handler pipeline construction

**Execution order:** Handlers execute in the order they are added (first added = outermost, receives request first).

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

## Result-Level Resilience

In addition to HTTP-level policies (Polly), you can apply retry logic at the Result level using `ResultResilienceExtensions`.

### Retry with Default Transient Error Policy

```csharp
using Voyager.Common.Proxy.Client.Extensions;

// Retry transient errors (Unavailable, Timeout) with exponential backoff
var result = await ResultResilienceExtensions.RetryAsync(
    () => _userService.GetUserAsync(id));

// Default: 3 attempts, 1s/2s/4s delays
```

### Custom Retry Policy

```csharp
// Custom max attempts and base delay
var result = await ResultResilienceExtensions.RetryAsync(
    () => _userService.GetUserAsync(id),
    ResultResilienceExtensions.TransientErrorPolicy(maxAttempts: 5, baseDelayMs: 500));

// Custom retry conditions
var policy = ResultResilienceExtensions.CustomRetryPolicy(
    maxAttempts: 5,
    shouldRetry: error => error.Type == ErrorType.Unavailable || error.Code == "RATE_LIMIT",
    delayStrategy: attempt => 500 * attempt);  // Linear backoff

var result = await ResultResilienceExtensions.RetryAsync(() => _userService.GetUserAsync(id), policy);
```

### Error Classification

According to ADR-007, errors are classified as:

| Classification | ErrorType | Retryable | Circuit Breaker |
|----------------|-----------|-----------|-----------------|
| **Transient** | Unavailable, Timeout | Yes | Counts |
| **Infrastructure** | Database, Unexpected | No | Counts |
| **Business** | Validation, NotFound, Permission, Unauthorized, Conflict, Business, Cancelled | No | Ignores |

Helper methods for classification:

```csharp
using Voyager.Common.Proxy.Client.Extensions;

// Check if error is transient (retryable)
if (error.IsTransient())
{
    // Retry logic
}

// Check if error should count towards circuit breaker
if (error.IsInfrastructureFailure())
{
    // Log infrastructure issue
}
```

### HTTP Status Code Mapping

| HTTP Status | ErrorType | Classification |
|-------------|-----------|----------------|
| 408 Request Timeout | Timeout | Transient |
| 429 Too Many Requests | Unavailable | Transient |
| 502 Bad Gateway | Unavailable | Transient |
| 503 Service Unavailable | Unavailable | Transient |
| 504 Gateway Timeout | Timeout | Transient |
| 500 Internal Server Error | Unexpected | Infrastructure |
| 400 Bad Request | Validation | Business |
| 401 Unauthorized | Unauthorized | Business |
| 403 Forbidden | Permission | Business |
| 404 Not Found | NotFound | Business |
| 409 Conflict | Conflict | Business |

### Combining with Polly

For comprehensive resilience, combine HTTP-level (Polly) and Result-level policies:

```csharp
// HTTP-level: Handle transport errors (connection refused, DNS failures)
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));

// Result-level: Handle semantic errors after HTTP succeeds
var result = await ResultResilienceExtensions.RetryAsync(
    () => _userService.GetUserAsync(id),
    ResultResilienceExtensions.TransientErrorPolicy(maxAttempts: 3, baseDelayMs: 1000));
```

### Built-in Resilience (Retry + Circuit Breaker)

The simplest way to add resilience is to configure it at registration time. Both retry and circuit breaker are applied automatically to all proxy calls.

The circuit breaker uses [Voyager.Common.Resilience](https://www.nuget.org/packages/Voyager.Common.Resilience/) internally and automatically:
- **Counts** infrastructure errors: `Unavailable`, `Timeout`, `Database`, `Unexpected`
- **Ignores** business errors: `Validation`, `NotFound`, `Permission`, etc.

#### ASP.NET Core

```csharp
// Program.cs - configure resilience at registration
builder.Services.AddServiceProxy<IPaymentService>(options =>
{
    options.BaseUrl = new Uri("https://payments.internal.com");

    // Enable retry with exponential backoff
    options.Resilience.Retry.Enabled = true;
    options.Resilience.Retry.MaxAttempts = 3;
    options.Resilience.Retry.BaseDelayMs = 1000;  // 1s, 2s, 4s

    // Enable circuit breaker
    options.Resilience.CircuitBreaker.Enabled = true;
    options.Resilience.CircuitBreaker.FailureThreshold = 5;
    options.Resilience.CircuitBreaker.OpenTimeout = TimeSpan.FromSeconds(30);
});

// Usage - resilience is automatic!
public class OrderService
{
    private readonly IPaymentService _paymentService;

    public OrderService(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    public async Task<Result<Order>> ProcessOrderAsync(OrderRequest request)
    {
        // Retry and circuit breaker are applied automatically
        var paymentResult = await _paymentService.ChargeAsync(request.PaymentDetails);

        return paymentResult.Bind(payment => CreateOrder(request, payment));
    }
}
```

#### OWIN / .NET Framework 4.8

```csharp
// Startup.cs - same configuration API
public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var services = new ServiceCollection();

        services.AddServiceProxy<IPaymentService>(options =>
        {
            options.BaseUrl = new Uri("https://payments.internal.com");

            options.Resilience.Retry.Enabled = true;
            options.Resilience.Retry.MaxAttempts = 3;

            options.Resilience.CircuitBreaker.Enabled = true;
            options.Resilience.CircuitBreaker.FailureThreshold = 5;
        });

        // Build ServiceProvider and configure OWIN...
    }
}

// Usage - same as ASP.NET Core
public class OrderService
{
    private readonly IPaymentService _paymentService;

    public async Task<Result<Order>> ProcessOrderAsync(OrderRequest request)
    {
        // Resilience is automatic
        return await _paymentService.ChargeAsync(request.PaymentDetails)
            .BindAsync(payment => CreateOrder(request, payment));
    }
}
```

#### Handling Circuit Breaker Open State

```csharp
var result = await _paymentService.ChargeAsync(request.PaymentDetails);

result.Switch(
    onSuccess: payment => ProcessPayment(payment),
    onFailure: error =>
    {
        if (error.Type == ErrorType.CircuitBreakerOpen)
        {
            _logger.LogWarning("Payment service circuit breaker open: {Message}", error.Message);
            return UseFallbackPaymentMethod();
        }
        return HandleError(error);
    });
```

### Manual Resilience (Advanced)

For more control, you can use `Voyager.Common.Resilience` extensions directly:

```csharp
using Voyager.Common.Resilience;

// Register proxy without built-in resilience
builder.Services.AddServiceProxy<IUserService>("https://api.example.com");

// Register shared circuit breaker
builder.Services.AddSingleton(new CircuitBreakerPolicy(
    failureThreshold: 5,
    openTimeout: TimeSpan.FromSeconds(30)));

// Apply manually in code
public async Task<Result<User>> GetUserAsync(int id)
{
    return await _userService.GetUserAsync(id)
        .BindWithCircuitBreakerAsync(user => EnrichUserAsync(user), _circuitBreaker);
}
```

#### Complete Manual Resilience Pattern

```csharp
using Voyager.Common.Proxy.Client.Extensions;
using Voyager.Common.Resilience;

// Retry transient errors, then apply circuit breaker
var result = await ResultResilienceExtensions.RetryAsync(
    () => _userService.GetUserAsync(id),
    ResultResilienceExtensions.TransientErrorPolicy(maxAttempts: 3, baseDelayMs: 500))
    .BindWithCircuitBreakerAsync(
        user => _externalService.EnrichUserDataAsync(user),
        _circuitBreaker);

// Handle circuit breaker open state
result.Switch(
    onSuccess: data => ProcessData(data),
    onFailure: error =>
    {
        if (error.Type == ErrorType.CircuitBreakerOpen)
        {
            _logger.LogWarning("Circuit breaker open: {Message}", error.Message);
            return GetCachedData(); // Fallback
        }
        _logger.LogError("Operation failed: {Error}", error);
    });
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

## Client-Side Request Validation

When `[ValidateRequest(ClientSide = true)]` is set, the proxy validates request parameters before making an HTTP call. This is an optimization to avoid network traffic for invalid requests.

### How It Works

```
Proxy Method Call
    │
    ├─► Check [ValidateRequest(ClientSide = true)]
    │     │
    │     └─► ValidateArguments()
    │           │
    │           ├─ IValidatableRequest.IsValid() → Result
    │           ├─ IValidatableRequestBool.IsValid() → bool
    │           └─ [ValidationMethod] → Result or bool
    │
    ├─► On failure: Return Result.Failure(Error.ValidationError)
    │     │
    │     └─► NO HTTP call made
    │
    └─► On success: Make HTTP request
          │
          └─► Server validates AGAIN (security)
```

### Example

```csharp
// Service interface with client-side validation
[ValidateRequest(ClientSide = true)]
public interface IPaymentService
{
    Task<Result<Payment>> CreatePaymentAsync(CreatePaymentRequest request);
}

// Request with validation
public class CreatePaymentRequest : IValidatableRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }

    public Result IsValid()
    {
        if (Amount <= 0)
            return Result.Failure(Error.ValidationError("Amount must be positive"));
        return Result.Success();
    }
}

// Usage
var result = await _paymentService.CreatePaymentAsync(new CreatePaymentRequest
{
    Amount = -10,  // Invalid!
    Currency = "USD"
});

// Result is failure - NO HTTP call was made
result.IsFailure;  // true
result.Error.Type;  // ErrorType.Validation
result.Error.Message;  // "Amount must be positive"
```

### When to Use Client-Side Validation

| Scenario | Use `ClientSide = true` |
|----------|-------------------------|
| High network latency | ✅ Save roundtrip time |
| Expensive validation | ❌ Validate once on server |
| Security-critical | ❌ Server always validates anyway |
| Shared request models | ✅ Reuse validation logic |
| Simple validation | ✅ Quick fail for obvious errors |

**Note:** Server-side validation ALWAYS happens regardless of `ClientSide` setting. Client validation is an optimization, not a security measure.

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
- `Voyager.Common.Resilience` - Circuit breaker pattern (used internally)
- `Microsoft.Extensions.Http` - HttpClientFactory

**net48 only:**
- `Castle.Core` - Dynamic proxy generation

**Optional:**
- `Microsoft.Extensions.Http.Polly` - HTTP-level retry policies

## Diagnostics and Observability

The proxy supports diagnostics events for logging, metrics, and observability. Events are emitted for all proxy operations including requests, retries, and circuit breaker state changes.

### Basic Setup with Logging

```csharp
// Install: dotnet add package Voyager.Common.Proxy.Diagnostics

using Voyager.Common.Proxy.Diagnostics;

// Register logging diagnostics
services.AddLogging(builder => builder.AddConsole());
services.AddProxyLoggingDiagnostics();

// Register your service proxy
services.AddServiceProxy<IUserService>("https://api.example.com");
```

### Adding User Context

To include user information (login, unit ID, unit type) in all diagnostic events:

```csharp
// Implement IProxyRequestContext
public class HttpContextRequestContext : IProxyRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextRequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserLogin => _httpContextAccessor.HttpContext?.User?.Identity?.Name;
    public string? UnitId => _httpContextAccessor.HttpContext?.User?.FindFirst("unit_id")?.Value;
    public string? UnitType => "Agent"; // or from claims/config
    public IReadOnlyDictionary<string, string>? CustomProperties => null;
}

// Register
services.AddHttpContextAccessor();
services.AddProxyRequestContext<HttpContextRequestContext>();
```

### Custom Diagnostics Handler

Create custom handlers for metrics, APM, or alerting:

```csharp
public class MetricsDiagnostics : ProxyDiagnosticsHandler
{
    private readonly IMetricsService _metrics;

    public MetricsDiagnostics(IMetricsService metrics) => _metrics = metrics;

    public override void OnRequestCompleted(RequestCompletedEvent e)
    {
        _metrics.RecordHistogram("proxy_duration_ms", e.Duration.TotalMilliseconds,
            new[] { ("service", e.ServiceName), ("method", e.MethodName) });
    }

    public override void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
    {
        if (e.NewState == "Open")
            _metrics.IncrementCounter("circuit_breaker_opened",
                new[] { ("service", e.ServiceName) });
    }
}

// Register multiple handlers
services.AddProxyDiagnostics<LoggingProxyDiagnostics>();
services.AddProxyDiagnostics<MetricsDiagnostics>();
```

### Available Events

| Event | When Emitted |
|-------|--------------|
| `OnRequestStarting` | Before HTTP request is sent |
| `OnRequestCompleted` | After response received (success or business error) |
| `OnRequestFailed` | When exception occurs |
| `OnRetryAttempt` | Before each retry attempt |
| `OnCircuitBreakerStateChanged` | When circuit breaker state changes |

## Related Packages

- **Voyager.Common.Proxy.Abstractions** - HTTP attributes (optional)
- **Voyager.Common.Proxy.Diagnostics** - Logging diagnostics handler
- **Voyager.Common.Proxy.Server** - Server-side endpoint generation
- **Voyager.Common.Results** - Result pattern library
- **Voyager.Common.Resilience** - Circuit breaker for Result types (included)

## License

MIT License - see [LICENSE](https://github.com/Voyager-Poland/voyager.common.proxy/blob/main/LICENSE) for details.
