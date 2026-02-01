# Voyager.Common.Proxy.Server.Owin

OWIN integration for Voyager.Common.Proxy.Server - automatically generates HTTP endpoints from service interfaces for .NET Framework 4.8.

## Installation

```powershell
Install-Package Voyager.Common.Proxy.Server.Owin
```

## Quick Start

```csharp
// Define your service interface
public interface IUserService
{
    Task<User> GetUserAsync(int id, CancellationToken cancellationToken);
    Task<User> CreateUserAsync(CreateUserRequest request);
    Task<IEnumerable<User>> SearchUsersAsync(string name, int? limit);
    Task DeleteUserAsync(int id);
}

// Configure in OWIN Startup
public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var container = new UnityContainer();
        container.RegisterType<IUserService, UserService>();

        // Using factory
        app.Use(ServiceProxyOwinMiddleware.Create<IUserService>(
            () => container.Resolve<IUserService>()));

        // Or using IServiceProvider
        app.Use(ServiceProxyOwinMiddleware.Create<IUserService>(serviceProvider));
    }
}
```

This generates the following endpoints:

| Method | Route | Parameters |
|--------|-------|------------|
| GET | `/User/GetUser/{id}` | `id` from route, `cancellationToken` injected |
| POST | `/User/CreateUser` | `request` from body |
| GET | `/User/SearchUsers` | `name`, `limit` from query string |
| DELETE | `/User/DeleteUser/{id}` | `id` from route |

## Features

- **Automatic endpoint generation** from service interfaces
- **Convention-based routing** matching `Voyager.Common.Proxy.Client`
- **Parameter binding** from route, query string, and request body
- **Result\<T\> support** - automatically unwraps successful results or returns appropriate error responses
- **Authorization support** - `[RequireAuthorization]` and `[AllowAnonymous]` attributes
- **Raw OWIN signatures** - uses `Func<IDictionary<string, object>, Task>` for maximum compatibility
- **Works with any OWIN host** - Katana, Microsoft.Owin, custom hosts

## Authorization

The middleware supports authorization using `[RequireAuthorization]` and `[AllowAnonymous]` attributes:

```csharp
// Require authorization for all methods
[RequireAuthorization]
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id);
    Task<Result<User>> CreateUserAsync(CreateUserRequest request);

    // Allow anonymous access to this method
    [AllowAnonymous]
    Task<Result<User>> GetPublicProfileAsync(int id);
}

// Require specific roles
[RequireAuthorization(Roles = "Admin,Manager")]
public interface IAdminService
{
    Task<Result> DeleteUserAsync(int id);
}
```

The middleware checks the OWIN environment for the authenticated user:
- `server.User`
- `owin.User`
- `Microsoft.Owin.Security.User` (Katana)

### Authorization Responses

- **401 Unauthorized** - User is not authenticated
- **403 Forbidden** - User is authenticated but doesn't have required role(s)

### OWIN Authentication Setup

Make sure authentication middleware runs before the service proxy middleware:

```csharp
public void Configuration(IAppBuilder app)
{
    // Authentication middleware first
    app.UseCookieAuthentication(new CookieAuthenticationOptions
    {
        AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie
    });

    // Then service proxy
    app.Use(ServiceProxyOwinMiddleware.Create<IUserService>(factory));
}
```

> **Note:** Policy-based authorization is an ASP.NET Core feature and is not fully supported in OWIN. When policies are specified, the middleware only checks if the user is authenticated.

## Permission Checking

For fine-grained permission control, use the options-based configuration with a permission checker. This runs **before** each method invocation:

```csharp
// Simple inline permission checking
app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
{
    options.ServiceFactory = () => container.Resolve<IVIPService>();
    options.PermissionChecker = async ctx =>
    {
        if (ctx.User?.Identity?.IsAuthenticated != true)
            return PermissionResult.Unauthenticated();

        // Check based on method and parameters
        if (ctx.Method.Name == "DeleteAsync" && !ctx.User.IsInRole("Admin"))
            return PermissionResult.Denied("Admin role required");

        return PermissionResult.Granted();
    };
}));
```

### Context-Aware Factory

When your service needs access to the request context (e.g., to inject per-request identity):

```csharp
app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
{
    options.ContextAwareFactory = env =>
    {
        var user = env["server.User"] as ClaimsPrincipal;
        var identity = PilotIdentityFactory.Create(user);
        return new VIPService(identity, actionModule);
    };
    options.PermissionChecker = async ctx =>
    {
        // Permission logic here
        return PermissionResult.Granted();
    };
}));
```

### Typed Permission Checker

For complex scenarios, implement `IServicePermissionChecker<TService>`:

```csharp
public class VIPServicePermissionChecker : IServicePermissionChecker<IVIPService>
{
    private readonly ActionModule _actionModule;

    public VIPServicePermissionChecker(ActionModule actionModule)
    {
        _actionModule = actionModule;
    }

    public async Task<PermissionResult> CheckPermissionAsync(PermissionContext context)
    {
        var identity = PilotIdentity.FromPrincipal(context.User);
        var action = _actionModule.GetActionForMethod(context.Method.Name);

        // Use existing Action pattern for permission checking
        var result = await action.CheckPermissionsOnlyAsync(
            BuildRequest(context.Parameters), identity);

        return result.IsSuccess
            ? PermissionResult.Granted()
            : PermissionResult.Denied(result.Error.Message);
    }
}

// Usage
app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
{
    options.ServiceFactory = () => container.Resolve<IVIPService>();
    options.PermissionCheckerInstance = container.Resolve<VIPServicePermissionChecker>();
}));
```

### PermissionContext

The permission checker receives a `PermissionContext` with:

| Property | Description |
|----------|-------------|
| `User` | The authenticated `IPrincipal` (may be null) |
| `ServiceType` | The service interface type |
| `Method` | The `MethodInfo` being invoked |
| `Endpoint` | The `EndpointDescriptor` with route info |
| `Parameters` | Dictionary of parameter name to deserialized value |
| `RawContext` | The OWIN environment dictionary |

### PermissionResult

Return values from permission checker:

```csharp
PermissionResult.Granted()              // Allow the request
PermissionResult.Denied("reason")       // 403 Forbidden
PermissionResult.Unauthenticated()      // 401 Unauthorized
```

## OWIN Compatibility

This package uses raw OWIN delegate signatures instead of `IAppBuilder` extensions for maximum compatibility with SDK-style projects. The middleware factory returns a standard OWIN middleware wrapper:

```csharp
Func<AppFunc, AppFunc> middleware = ServiceProxyOwinMiddleware.Create<IUserService>(factory);
```

## Diagnostics and Observability

The OWIN middleware supports diagnostic events for logging and observability. Configure diagnostics through the options.

### Basic Setup

```csharp
using Voyager.Common.Proxy.Diagnostics;
using Microsoft.Extensions.Logging;

public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<LoggingProxyDiagnostics>();
        var diagnosticsHandler = new LoggingProxyDiagnostics(logger);

        app.Use(ServiceProxyOwinMiddleware.Create<IUserService>(options =>
        {
            options.ServiceFactory = () => container.Resolve<IUserService>();
            options.DiagnosticsHandlers = new IProxyDiagnostics[] { diagnosticsHandler };
        }));
    }
}
```

### Adding User Context

To include user information in diagnostic events:

```csharp
public class OwinProxyRequestContext : IProxyRequestContext
{
    private readonly IDictionary<string, object> _environment;

    public OwinProxyRequestContext(IDictionary<string, object> environment)
    {
        _environment = environment;
    }

    public string? UserLogin
    {
        get
        {
            var user = _environment.TryGetValue("server.User", out var u) ? u as ClaimsPrincipal : null;
            return user?.Identity?.Name;
        }
    }

    public string? UnitId
    {
        get
        {
            var user = _environment.TryGetValue("server.User", out var u) ? u as ClaimsPrincipal : null;
            return user?.FindFirst("unit_id")?.Value;
        }
    }

    public string? UnitType => "Agent";
    public IReadOnlyDictionary<string, string>? CustomProperties => null;
}

// Configure with request context factory
app.Use(ServiceProxyOwinMiddleware.Create<IUserService>(options =>
{
    options.ServiceFactory = () => container.Resolve<IUserService>();
    options.DiagnosticsHandlers = new IProxyDiagnostics[] { diagnosticsHandler };
    options.RequestContextFactory = env => new OwinProxyRequestContext(env);
}));
```

### Custom Diagnostics Handler

```csharp
public class MetricsDiagnostics : ProxyDiagnosticsHandler
{
    private readonly IMetricsService _metrics;

    public MetricsDiagnostics(IMetricsService metrics) => _metrics = metrics;

    public override void OnRequestCompleted(RequestCompletedEvent e)
    {
        _metrics.RecordHistogram("owin_request_duration_ms", e.Duration.TotalMilliseconds,
            new[] { ("service", e.ServiceName), ("method", e.MethodName) });
    }
}

// Multiple handlers
options.DiagnosticsHandlers = new IProxyDiagnostics[]
{
    new LoggingProxyDiagnostics(logger),
    new MetricsDiagnostics(metricsService)
};
```

### Server-Side Events

| Event | When Emitted |
|-------|--------------|
| `OnRequestStarting` | When request is received |
| `OnRequestCompleted` | After response is sent (success or business error) |
| `OnRequestFailed` | When exception occurs during processing |

> **Note:** Server-side does not emit `OnRetryAttempt` or `OnCircuitBreakerStateChanged` - these are client-side patterns.

## Target Framework

- `net48` (.NET Framework 4.8)

## Related Packages

- **Voyager.Common.Proxy.Client** - Client-side proxy generation
- **Voyager.Common.Proxy.Server.AspNetCore** - ASP.NET Core server integration
