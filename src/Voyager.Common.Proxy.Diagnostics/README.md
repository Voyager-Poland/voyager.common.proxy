# Voyager.Common.Proxy.Diagnostics

Logging diagnostics handler for Voyager.Common.Proxy. Integrates with `Microsoft.Extensions.Logging` to provide structured logging for all proxy operations.

## Installation

```bash
dotnet add package Voyager.Common.Proxy.Diagnostics
```

## Usage

```csharp
// Register logging and diagnostics
services.AddLogging(builder => builder.AddConsole());
services.AddProxyLoggingDiagnostics();

// Register your service proxy
services.AddServiceProxy<IUserService>("https://api.example.com");

// Optionally register user context provider
services.AddProxyRequestContext<HttpContextRequestContext>();
```

## Log Output

The handler produces structured logs with the following information:

### Request Starting (Debug)
```
Proxy request starting: GET /users/123 [IUserService.GetUserAsync] CorrelationId=abc-123 User=jan.kowalski Unit=12345/Agent
```

### Request Completed - Success (Debug)
```
Proxy request completed: GET /users/123 200 in 45ms [IUserService.GetUserAsync] CorrelationId=abc-123 User=jan.kowalski Unit=12345/Agent
```

### Request Completed - Failure (Warning)
```
Proxy request failed: GET /users/123 404 in 30ms [IUserService.GetUserAsync] Error=NotFound: User not found CorrelationId=abc-123 User=jan.kowalski Unit=12345/Agent
```

### Request Exception (Error)
```
Proxy request exception: GET /users/123 in 5000ms [IUserService.GetUserAsync] Exception=HttpRequestException: Connection refused CorrelationId=abc-123 User=jan.kowalski Unit=12345/Agent
```

### Retry Attempt (Warning)
```
Proxy retry attempt 1/3 for [IUserService.GetUserAsync] after Timeout: Request timed out. Waiting 1000ms. CorrelationId=abc-123 User=jan.kowalski Unit=12345/Agent
```

### Circuit Breaker State Changed (Warning/Information)
```
Circuit breaker OPENED for IUserService: Closed -> Open. Failures=5. LastError=Unavailable: Connection refused User=jan.kowalski Unit=12345/Agent
```

## User Context

To include user information in logs, implement `IProxyRequestContext`:

```csharp
public class HttpContextRequestContext : IProxyRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextRequestContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? UserLogin => _httpContextAccessor.HttpContext?.User?.Identity?.Name;
    public string? UnitId => _httpContextAccessor.HttpContext?.User?.FindFirst("unit_id")?.Value;
    public string? UnitType => "Agent";
    public IReadOnlyDictionary<string, string>? CustomProperties => null;
}

// Register
services.AddHttpContextAccessor();
services.AddProxyRequestContext<HttpContextRequestContext>();
```
