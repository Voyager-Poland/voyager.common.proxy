# Voyager.Common.Proxy.Server.Core

Core logic for service scanning, endpoint metadata building, and request dispatching.

## Overview

This package contains the shared implementation used by both `Voyager.Common.Proxy.Server.AspNetCore` and `Voyager.Common.Proxy.Server.Owin` to transform service interfaces into HTTP endpoints.

## Key Types

- **`ServiceScanner`** - Scans service interfaces and builds endpoint descriptors using the same routing conventions as `Voyager.Common.Proxy.Client`
- **`RequestDispatcher`** - Invokes service methods based on HTTP requests and writes responses
- **`RequestValidator`** - Validates request parameters before method execution
- **`ParameterBinder`** - Binds request parameters from route values, query strings, and request body
- **`EndpointMatcher`** - Matches incoming HTTP requests to registered endpoints

## Routing Conventions

The scanner uses the same conventions as the client library:

| Method Prefix | HTTP Method | Route Template |
|---------------|-------------|----------------|
| `Get`, `Find`, `Search` | GET | `/{ServiceName}/{MethodName}/{id?}` |
| `Create`, `Add`, `Insert` | POST | `/{ServiceName}/{MethodName}` |
| `Update`, `Modify`, `Set` | PUT | `/{ServiceName}/{MethodName}/{id}` |
| `Delete`, `Remove` | DELETE | `/{ServiceName}/{MethodName}/{id}` |
| Other | POST | `/{ServiceName}/{MethodName}` |

## Usage

This package is typically not used directly. Instead, use one of the platform-specific packages:

- **ASP.NET Core (.NET 6.0+)**: `Voyager.Common.Proxy.Server.AspNetCore`
- **OWIN (.NET Framework 4.8)**: `Voyager.Common.Proxy.Server.Owin`

## Error Type to HTTP Status Code Mapping

According to ADR-007, `Result<T>.Error` types are mapped to HTTP status codes:

| ErrorType | HTTP Status | Classification |
|-----------|-------------|----------------|
| `Validation` | 400 Bad Request | Business |
| `Business` | 400 Bad Request | Business |
| `Unauthorized` | 401 Unauthorized | Business |
| `Permission` | 403 Forbidden | Business |
| `NotFound` | 404 Not Found | Business |
| `Conflict` | 409 Conflict | Business |
| `Cancelled` | 499 Client Closed | Business |
| `Timeout` | 504 Gateway Timeout | Transient |
| `Unavailable` | 503 Service Unavailable | Transient |
| `CircuitBreakerOpen` | 503 Service Unavailable | Transient |
| `Database` | 500 Internal Server Error | Infrastructure |
| `Unexpected` | 500 Internal Server Error | Infrastructure |

**Classification meaning:**
- **Business errors**: Client should NOT retry (invalid input, not found, no permission)
- **Transient errors**: Client MAY retry (temporary unavailability)
- **Infrastructure errors**: Client should NOT retry, but circuit breaker should count

## Server-to-Server Calls

When your server needs to call other services, use `Voyager.Common.Proxy.Client` with built-in resilience:

```csharp
// In your server's Startup/Program.cs
services.AddServiceProxy<IPaymentService>(options =>
{
    options.BaseUrl = new Uri("https://payments.internal.com");
    options.Resilience.Retry.Enabled = true;
    options.Resilience.CircuitBreaker.Enabled = true;
});
```

See [Voyager.Common.Proxy.Client](../Voyager.Common.Proxy.Client/README.md) for details.

## Request Validation

The `RequestDispatcher` supports automatic validation of request parameters when `[ValidateRequest]` attribute is present on the method or interface.

### How It Works

1. Before invoking the service method, `RequestValidator` checks all parameters
2. Parameters implementing `IValidatableRequest` or `IValidatableRequestBool` are validated
3. Parameters with `[ValidationMethod]` attribute have that method invoked
4. On validation failure, `ArgumentException` is thrown and caught by existing error handling
5. HTTP 400 response is returned with `ErrorType="Validation"`

### Validation Flow

```
HTTP Request
    │
    ▼
RequestDispatcher
    │
    ├─► Deserialize parameters
    │
    ├─► Check [ValidateRequest] attribute
    │     │
    │     └─► RequestValidator.ValidateParameters()
    │           │
    │           ├─ IValidatableRequest.IsValid() → Result
    │           ├─ IValidatableRequestBool.IsValid() → bool
    │           └─ [ValidationMethod] → Result or bool
    │
    ├─► On failure: throw ArgumentException
    │     │
    │     └─► HTTP 400 + ErrorType="Validation"
    │
    └─► On success: Invoke service method
```

### Supported Validation Approaches

| Approach | Detection | Performance |
|----------|-----------|-------------|
| `IValidatableRequest` | Interface cast | Fast |
| `IValidatableRequestBool` | Interface cast | Fast |
| `[ValidationMethod]` | Reflection | Slower |

### Example

```csharp
// Service interface with validation
[ValidateRequest]
public interface IPaymentService
{
    Task<Result<Payment>> CreatePaymentAsync(CreatePaymentRequest request);
}

// Request model with validation
public class CreatePaymentRequest : IValidatableRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }

    public Result IsValid()
    {
        if (Amount <= 0)
            return Result.Failure(Error.ValidationError("Amount must be positive"));
        if (string.IsNullOrEmpty(Currency))
            return Result.Failure(Error.ValidationError("Currency is required"));
        return Result.Success();
    }
}
```

When an invalid request is received, the response will be:
```
HTTP 400 Bad Request
ErrorType: Validation
ErrorMessage: Amount must be positive
```

## Target Framework

- `netstandard2.0` - Compatible with .NET Framework 4.6.1+, .NET Core 2.0+, and .NET 5+
