# Voyager.Common.Proxy.Diagnostics.ApplicationInsights

Application Insights diagnostics handler for Voyager.Common.Proxy. Sends proxy telemetry to Azure Application Insights as dependency calls, exceptions, and custom events.

## Installation

```bash
dotnet add package Voyager.Common.Proxy.Diagnostics.ApplicationInsights
```

## Usage

### Basic registration

```csharp
services.AddApplicationInsightsTelemetry();
services.AddProxyApplicationInsightsDiagnostics();
services.AddServiceProxy<IUserService>("https://api.example.com");
```

### With Cloud.RoleName (recommended for multi-service environments)

```csharp
services.AddProxyApplicationInsightsDiagnostics(options =>
{
    options.CloudRoleName = "MyService-Production";
});
```

## Event → Telemetry Mapping

| Proxy Event | AI Telemetry Type | Name / Details |
|---|---|---|
| `OnRequestCompleted` | `DependencyTelemetry` | type=VoyagerProxy, success/failure, duration, resultCode |
| `OnRequestFailed` | `ExceptionTelemetry` + `DependencyTelemetry` | Exception details + failed dependency |
| `OnRetryAttempt` | `EventTelemetry` | "ProxyRetryAttempt" with attempt details |
| `OnCircuitBreakerStateChanged` | `EventTelemetry` | "ProxyCircuitBreakerStateChanged" with state info |
| `OnRequestStarting` | *(no-op)* | Covered by DependencyTelemetry in Completed/Failed |

## Custom Properties (→ customDimensions in AI)

All telemetry items include these properties where available:

| Property | Source |
|---|---|
| `ServiceName` | Interface name (e.g. `IUserService`) |
| `MethodName` | Method name (e.g. `GetUserAsync`) |
| `HttpMethod` | HTTP method (GET, POST, etc.) |
| `Url` | Request URL (relative path) |
| `UserLogin` | Current user's login |
| `UnitId` | Organizational unit identifier |
| `UnitType` | Organizational unit type |
| *(custom)* | All entries from `IProxyRequestContext.CustomProperties` |

## W3C Trace Context

Telemetry items have `operation_Id` set to the W3C TraceId and `operation_ParentId` set to the SpanId, enabling correlation with distributed traces.

## Environment Separation

Use `CloudRoleName` to distinguish services/environments in Application Insights. Combined with per-environment Connection Strings, this provides full environment isolation:

```csharp
// appsettings.Production.json → AI Connection String for Production resource
// appsettings.Staging.json → AI Connection String for Staging resource
services.AddProxyApplicationInsightsDiagnostics(options =>
{
    options.CloudRoleName = $"MyService-{environment}";
});
```
