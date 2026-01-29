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
- **Raw OWIN signatures** - uses `Func<IDictionary<string, object>, Task>` for maximum compatibility
- **Works with any OWIN host** - Katana, Microsoft.Owin, custom hosts

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
