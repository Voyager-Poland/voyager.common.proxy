# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **Breaking:** HTTP 429 now returns `ErrorType.TooManyRequests` instead of `ErrorType.Unavailable` (ADR-009)
- Upgraded `Voyager.Common.Results` dependency to 1.7.0-preview.2
- Upgraded `Voyager.Common.Resilience` dependency to 1.7.0-preview.2
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
- ADR-008: Diagnostics Strategy now uses callback APIs from Voyager.Common.Results/Resilience 1.7.0-preview.2:
  - `CircuitBreakerPolicy.OnStateChanged` callback for circuit breaker state changes
  - `BindWithRetryAsync(..., onRetryAttempt)` callback for retry attempt notifications
