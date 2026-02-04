# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.7.5] - 2026-02-04

### Added

- **Parameterized constructor support in ParameterBinder** (Server.Core):
  - Complex types with parameterized constructors (e.g., C# records) are now supported for route/query binding
  - Constructor parameters are filled from route values and query parameters (case-insensitive)
  - Supports default parameter values and nullable types
  - Falls back to parameterless constructor if available

## [1.7.3] - 2026-02-03

### Added

- **DelegatingHandlerFactories in ServiceProxyOptions** - alternative way to add HTTP message handlers:
  - New `DelegatingHandlerFactories` property allows registering handler factories directly in options
  - Handlers are built manually bypassing `IHttpClientFactory.AddHttpMessageHandler`
  - More reliable in Unity/DI container bridging scenarios where `AddHttpMessageHandler` doesn't work
  - Handlers execute in order added (first added = outermost handler)
  - Example: `options.DelegatingHandlerFactories.Add(sp => sp.GetRequiredService<AuthorizationHandler>())`

## [1.7.2] - 2026-02-03

### Fixed

- **Defensive fallback for HttpClient BaseAddress** in `ServiceCollectionExtensions`:
  - Added fallback to manually set `BaseAddress` when `IHttpClientFactory` doesn't properly configure it
  - Fixes issues in Unity/DI container bridging scenarios where `AddHttpClient` configuration is not applied
  - Ensures proxy works correctly even when DI integration has configuration issues

## [1.7.1] - 2026-02-03

### Added

- **Complex type support for GET/DELETE requests** in `RouteBuilder`:
  - Properties from complex type parameters are now extracted and used as query string parameters
  - Route template placeholders (e.g., `{UserId}`) can bind to properties from complex types
  - Nested complex properties are automatically skipped (only simple types are extracted)
  - Null properties are omitted from query string
  - Example: `GetUsersAsync(SearchQuery query)` with `SearchQuery { Name = "john", Page = 1 }` → `GET /get-users?Name=john&Page=1`
  - Example: `[HttpGet("users/{UserId}/orders")] GetUserOrdersAsync(UserOrdersQuery query)` with `UserId = 123, Status = "pending"` → `GET /users/123/orders?Status=pending`

- **ADR-011: Automatic Request Validation** - complete implementation:
  - `IValidatableRequest` interface for validation returning `Result`
  - `IValidatableRequestBool` interface for simple boolean validation
  - `[ValidateRequest]` attribute to enable validation on methods/interfaces
  - `[ValidationMethod]` attribute to mark existing validation methods
  - Server-side validation in `RequestDispatcher` before method invocation
  - Client-side validation in `HttpMethodInterceptor` with `[ValidateRequest(ClientSide = true)]`
  - Client validation prevents HTTP call for invalid requests (optimization)
  - Server always validates regardless of `ClientSide` setting (security)

## [1.6.0] - 2026-02-03

### Changed

- **Breaking:** HTTP 429 now returns `ErrorType.TooManyRequests` instead of `ErrorType.Unavailable` (ADR-009)
- Upgraded `Voyager.Common.Results` dependency to 1.7.1
- Upgraded `Voyager.Common.Resilience` dependency to 1.7.1
- `AspNetCoreResponseWriter` and `OwinResponseWriter` now use centralized `ErrorType.ToHttpStatusCode()` extension
- `HttpMethodInterceptor` now uses `error.Type.IsTransient()` from `Voyager.Common.Results.Extensions`

### Deprecated

- `ResultResilienceExtensions.IsTransient(Error)` - use `error.Type.IsTransient()` instead
- `ResultResilienceExtensions.IsInfrastructureFailure(Error)` - use `error.Type.ShouldCountForCircuitBreaker()` instead

### Removed

- Removed duplicate `IsTransientError()` method from `HttpMethodInterceptor`
- Removed ~50 lines of duplicated error classification logic

### Added

- New error types supported: `TooManyRequests`, `CircuitBreakerOpen` in transient classification
- Server projects now reference `Voyager.Common.Results` for centralized error handling
- **ADR-008: Diagnostics and Observability** - complete implementation:
  - `IProxyDiagnostics` interface for receiving proxy events
  - `IProxyRequestContext` interface for user context (login, unit ID, unit type)
  - Event types: `RequestStartingEvent`, `RequestCompletedEvent`, `RequestFailedEvent`, `RetryAttemptEvent`, `CircuitBreakerStateChangedEvent`
  - `ProxyDiagnosticsHandler` abstract base class for easy handler implementation
  - New package `Voyager.Common.Proxy.Diagnostics` with `LoggingProxyDiagnostics`
  - DI extensions: `AddProxyDiagnostics<T>()`, `AddProxyRequestContext<T>()`, `AddProxyLoggingDiagnostics()`
  - All events include user context for request counting per user/unit
  - **Server-side diagnostics**: `RequestDispatcher` now emits events for ASP.NET Core and OWIN servers
  - ASP.NET Core: diagnostics automatically resolved from DI
  - OWIN: `ServiceProxyOptions<T>.DiagnosticsHandlers` and `RequestContextFactory` properties
