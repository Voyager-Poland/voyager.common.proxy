# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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
- Upgraded `Voyager.Common.Results` dependency to 1.7.0
- Upgraded `Voyager.Common.Resilience` dependency to 1.7.0
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
