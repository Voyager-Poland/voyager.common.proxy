# Voyager.Common.Proxy.Diagnostics

Diagnostics handlers for Voyager.Common.Proxy. Provides two built-in `IProxyDiagnostics` implementations for logging proxy operations.

## Installation

```bash
dotnet add package Voyager.Common.Proxy.Diagnostics
```

## Usage

### LoggingProxyDiagnostics (via ILogger)

Uses `Microsoft.Extensions.Logging` with structured message templates. Works with any `ILogger` provider (Console, Serilog, NLog, etc.).

```csharp
services.AddLogging(builder => builder.AddConsole());
services.AddProxyLoggingDiagnostics();
services.AddServiceProxy<IUserService>("https://api.example.com");
```

### ConsoleProxyDiagnostics (direct Console.WriteLine)

Writes directly to `Console.WriteLine` using string interpolation. No dependency on any logging framework - useful for quick debugging or environments without a configured logging provider.

```csharp
services.AddProxyConsoleDiagnostics();
services.AddServiceProxy<IUserService>("https://api.example.com");
```

### User context (optional)

```csharp
services.AddProxyRequestContext<HttpContextRequestContext>();
```

## Log Output

The handler produces structured logs with W3C Trace Context fields for distributed tracing:

### Request Starting (Debug)
```
Proxy request starting: GET /users/123 [IUserService.GetUserAsync] TraceId=abc123def456789012345678901234ab SpanId=1234567890abcdef ParentSpanId=(root) User=jan.kowalski Unit=12345/Agent
```

### Request Completed - Success (Debug)
```
Proxy request completed: GET /users/123 200 in 45ms [IUserService.GetUserAsync] TraceId=abc123... SpanId=1234... ParentSpanId=(root) User=jan.kowalski Unit=12345/Agent
```

### Request Completed - Failure (Warning)
```
Proxy request failed: GET /users/123 404 in 30ms [IUserService.GetUserAsync] Error=NotFound: User not found TraceId=abc123... SpanId=1234... ParentSpanId=(root) User=jan.kowalski Unit=12345/Agent
```

### Request Exception (Error)
```
Proxy request exception: GET /users/123 in 5000ms [IUserService.GetUserAsync] Exception=HttpRequestException: Connection refused TraceId=abc123... SpanId=1234... ParentSpanId=(root) User=jan.kowalski Unit=12345/Agent
```

### Retry Attempt (Warning)
```
Proxy retry attempt 1/3 for [IUserService.GetUserAsync] after Timeout: Request timed out. Waiting 1000ms. TraceId=abc123... SpanId=1234... ParentSpanId=(root) User=jan.kowalski Unit=12345/Agent
```

### Circuit Breaker State Changed (Warning/Information)
```
Circuit breaker OPENED for IUserService: Closed -> Open. Failures=5. LastError=Unavailable: Connection refused User=jan.kowalski Unit=12345/Agent
```

## W3C Trace Context

All diagnostic events include W3C Trace Context fields:

| Field | Description |
|-------|-------------|
| `TraceId` | 32-character hex ID for the entire trace (spans multiple services) |
| `SpanId` | 16-character hex ID for this specific operation |
| `ParentSpanId` | 16-character hex ID of the parent span (null for root spans) |

When `Activity.Current` is available (e.g., with OpenTelemetry), trace context is automatically captured. Otherwise, new IDs are generated.

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
