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

## OWIN Compatibility

This package uses raw OWIN delegate signatures instead of `IAppBuilder` extensions for maximum compatibility with SDK-style projects. The middleware factory returns a standard OWIN middleware wrapper:

```csharp
Func<AppFunc, AppFunc> middleware = ServiceProxyOwinMiddleware.Create<IUserService>(factory);
```

## Target Framework

- `net48` (.NET Framework 4.8)

## Related Packages

- **Voyager.Common.Proxy.Client** - Client-side proxy generation
- **Voyager.Common.Proxy.Server.AspNetCore** - ASP.NET Core server integration
