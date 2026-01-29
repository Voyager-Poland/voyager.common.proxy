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
    Task<User> GetUserAsync(int id, CancellationToken cancellationToken);
    Task<User> CreateUserAsync(CreateUserRequest request);
    Task<IEnumerable<User>> SearchUsersAsync(string? name, int? limit);
    Task DeleteUserAsync(int id);
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
| GET | `/User/GetUser/{id}` | `id` from route, `cancellationToken` injected |
| POST | `/User/CreateUser` | `request` from body |
| GET | `/User/SearchUsers` | `name`, `limit` from query string |
| DELETE | `/User/DeleteUser/{id}` | `id` from route |

## Features

- **Automatic endpoint generation** from service interfaces
- **Convention-based routing** matching `Voyager.Common.Proxy.Client`
- **Parameter binding** from route, query string, and request body
- **Result\<T\> support** - automatically unwraps successful results or returns appropriate error responses
- **CancellationToken injection** - automatically passed from `HttpContext.RequestAborted`
- **Minimal API integration** - uses ASP.NET Core's endpoint routing

## Service Resolution

Services are resolved from `IServiceProvider` for each request:

```csharp
// With factory
app.MapServiceProxy<IUserService>(() => new UserService());

// With service provider (default)
app.MapServiceProxy<IUserService>();
```

## Target Frameworks

- `net6.0`
- `net8.0`
