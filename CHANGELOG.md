# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.10.0] - 2026-02-23

### Added

- **Roslyn analyzer package** (`Voyager.Common.Proxy.Analyzers`):
  - New `VP0001` diagnostic (Error): detects array/collection of simple types (`int[]`, `List<string>`, `IEnumerable<Guid>`, etc.) used as parameters on GET or DELETE proxy interface methods — a pattern that causes silent data loss at runtime
  - Supports both attribute-based (`[HttpGet]`, `[HttpDelete]`) and convention-based (method name prefixes: `Get*`, `Find*`, `List*`, `Search*`, `Delete*`, `Remove*`) HTTP method detection
  - Recognizes proxy service interfaces by `[ServiceRoute]` attribute or presence of any `[HttpMethod]` derivative on methods
  - Code Fix: **Change to [HttpPost]** — replaces `[HttpGet]`/`[HttpDelete]` with `[HttpPost]` (preserving route template), or adds `[HttpPost]` for convention-based methods
  - Automatically delivered via `Voyager.Common.Proxy.Abstractions` — any project referencing Abstractions gets the analyzer with zero configuration
  - 28 tests: 14 positive (diagnostic expected), 10 negative (no diagnostic), 4 code fix scenarios
  - See [ADR-015](docs/adr/ADR-015-Array-Collection-Query-Parameter-Support.md) for design rationale (Etap 1)

## [1.9.0] - 2026-02-19

### Added

- **Application Insights diagnostics package** (`Voyager.Common.Proxy.Diagnostics.ApplicationInsights`):
  - New `IProxyDiagnostics` implementation that sends proxy events to Azure Application Insights
  - `OnRequestCompleted` → `DependencyTelemetry` (type `VoyagerProxy`)
  - `OnRequestFailed` → `ExceptionTelemetry` + failed `DependencyTelemetry`
  - `OnRetryAttempt` → `EventTelemetry` (`ProxyRetryAttempt`)
  - `OnCircuitBreakerStateChanged` → `EventTelemetry` (`ProxyCircuitBreakerStateChanged`)
  - W3C Trace Context correlation (`operation_Id`, `operation_ParentId`) with `ParentSpanId` support
  - Configurable `CloudRoleName` for Application Map separation
  - DI extensions: `services.AddProxyApplicationInsightsDiagnostics()`
  - All handler methods wrapped in try/catch — diagnostics never affects proxy logic
  - See [ADR-014](docs/adr/ADR-014-ApplicationInsights-Diagnostics.md) for design rationale

### Changed

- **NuGet package updates**:
  - `Voyager.Common.Results` 1.8.0 → 1.9.0 (Abstractions, Server.Core, Client)
  - `Voyager.Common.Resilience` 1.8.0 → 1.9.0 (Client)

## [1.8.0] - 2026-02-18

### Added

- **Custom Content-Type support** (Abstractions, Server.Core, Server.AspNetCore, Server.Owin, Swagger, Client):
  - New `[ProducesContentType("text/html")]` attribute for methods returning non-JSON responses
  - When applied to a method returning `Result<string>`, the raw string value is written directly without JSON serialization
  - Error responses remain `application/json` regardless of the attribute
  - Fail-fast validation at scan time: `[ProducesContentType]` on non-`Result<string>` methods throws `InvalidOperationException`
  - Swagger generates correct content-type and inline string schema for decorated methods
  - Client proxy: `ResultMapper` detects non-JSON Content-Type in responses and returns raw string without JSON deserialization
  - Enables migration of payment callback endpoints (ePay) that require `text/html` responses
  - See [ADR-013](docs/adr/ADR-013-Custom-Content-Type-Support.md) for design rationale
- **Atlassian Compass** component registration (`compass.yml`)

### Fixed

- **OWIN WriteRawAsync**: Added `charset=utf-8` to Content-Type header for parity with ASP.NET Core
- **IDE0060**: Removed unused parameter warnings in Swagger.Core

### Changed

- **NuGet package updates**:
  - `Voyager.Common.Results` 1.7.1 → 1.8.0 (Abstractions, Server.Core, Client)
  - `Voyager.Common.Resilience` 1.7.1 → 1.8.0 (Client)
  - `xunit` 2.9.2 → 2.9.3 (all test projects)
  - `coverlet.collector` 6.0.0/6.0.2 → 6.0.4 (all test projects)
  - `Microsoft.NET.Test.Sdk` 17.8.0 → 17.12.0 (Server.Tests, Swagger.Core.Tests)
  - `xunit.runner.visualstudio` 2.5.3 → 2.8.2 (Server.Tests, Swagger.Core.Tests)
- **Cleanup**: Removed duplicate `Voyager.Common.Results` references from Server.AspNetCore and Server.Owin (already transitive via Server.Core)
- Added `.editorconfig` with tab indentation and fixed `CS0104` HttpMethod ambiguity

## [1.7.7] - 2026-02-06

### Added

- **Empty route prefix support** (Abstractions):
  - New `ServiceRouteAttribute.NoPrefix` constant for services without a route prefix
  - `[ServiceRoute("")]` and `[ServiceRoute(ServiceRouteAttribute.NoPrefix)]` are now allowed
  - Enables integration with external APIs that expose endpoints directly under root path (e.g., `/NewOrder` instead of `/service-name/NewOrder`)
  - Whitespace-only prefixes are still rejected (likely a mistake)
  - See [ADR-012](docs/adr/ADR-012-Empty-ServiceRoute-Prefix.md) for design rationale

## [1.7.6] - 2026-02-06

### Added

- **ConsoleProxyDiagnostics** (Diagnostics):
  - New `IProxyDiagnostics` implementation that writes directly to `Console.WriteLine` using string interpolation
  - Works independently of any logging framework (no `ILogger` dependency)
  - Useful when Serilog or other structured logging providers are not available
  - DI extension: `services.AddProxyConsoleDiagnostics()`
  - Accepts optional `TextWriter` for output redirection
- **Diagnostics test project** (`Voyager.Common.Proxy.Diagnostics.Tests`):
  - Tests for `ConsoleProxyDiagnostics` verifying output for all 5 event types and null fallbacks
  - Tests for `LoggingProxyDiagnostics` proving structured message templates work correctly with any standard `ILogger` implementation (not just Serilog)

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
