# Voyager.Common.Proxy.Diagnostics.TraceContext

Integration package that adds W3C Trace Context propagation to Voyager.Common.Proxy diagnostics.

## Installation

```bash
dotnet add package Voyager.Common.Proxy.Diagnostics.TraceContext
```

## Usage

### Implement ITraceContextAccessor

This package provides an `ITraceContextAccessor` interface that you need to implement to provide trace context:

```csharp
public class MyTraceContextAccessor : ITraceContextAccessor
{
    public string? TraceId => Activity.Current?.TraceId.ToString();
    public string? SpanId => Activity.Current?.SpanId.ToString();
    public string? ParentSpanId => Activity.Current?.ParentSpanId.ToString();
}
```

### ASP.NET Core

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Register your trace context accessor
builder.Services.AddSingleton<ITraceContextAccessor, MyTraceContextAccessor>();

// Add proxy with TraceContext integration
builder.Services.AddProxyTraceContext();  // <-- Adds IProxyRequestContext with trace info
builder.Services.AddServiceProxy<IUserService>("https://api.example.com");
builder.Services.AddProxyLoggingDiagnostics();

var app = builder.Build();
app.Run();
```

### With Existing User Context

If you already have a custom `IProxyRequestContext` for user information:

```csharp
// Your existing user context
public class HttpContextRequestContext : IProxyRequestContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public string? UserLogin => _httpContextAccessor.HttpContext?.User?.Identity?.Name;
    public string? UnitId => _httpContextAccessor.HttpContext?.User?.FindFirst("unit_id")?.Value;
    public string? UnitType => "Agent";
    public IReadOnlyDictionary<string, string>? CustomProperties => null;
}

// Registration - wraps your context with trace information
builder.Services.AddSingleton<ITraceContextAccessor, MyTraceContextAccessor>();
builder.Services.AddProxyTraceContext<HttpContextRequestContext>();
```

### OWIN (.NET Framework 4.8)

```csharp
// Startup.cs
public void Configuration(IAppBuilder app)
{
    // Create accessor (implement ITraceContextAccessor)
    var accessor = new MyTraceContextAccessor();

    // Proxy with TraceContext
    app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
    {
        options.ServiceFactory = () => container.Resolve<IVIPService>();
        options.RequestContextFactory = OwinTraceContextExtensions.CreateRequestContextFactory(accessor);
        options.DiagnosticsHandlers = new IProxyDiagnostics[]
        {
            new LoggingProxyDiagnostics(logger)
        };
    }));
}
```

## How It Works

This package provides:

| Component | Description |
|-----------|-------------|
| `ITraceContextAccessor` | Interface for providing trace context from your tracing infrastructure |
| `TraceContextProxyRequestContext` | Wraps `IProxyRequestContext` and adds trace info from `ITraceContextAccessor` |
| `TraceContextHelper` | Extracts TraceId/SpanId/ParentSpanId from `ITraceContextAccessor` |
| `AddProxyTraceContext()` | DI extension for ASP.NET Core |
| `OwinTraceContextExtensions` | Factory helpers for OWIN integration |

## Trace Context Fields

The integration adds these W3C Trace Context fields to diagnostic events via `CustomProperties`:

| Field | Description |
|-------|-------------|
| `trace.id` | 32-character hex ID for the entire distributed trace |
| `span.id` | 16-character hex ID for this specific operation |
| `parent.span.id` | 16-character hex ID of the parent span (null for root spans) |

## Related Packages

- **Voyager.Common.Proxy.Diagnostics** - Logging diagnostics handler
- **Voyager.Common.Proxy.Client** - Client-side proxy
