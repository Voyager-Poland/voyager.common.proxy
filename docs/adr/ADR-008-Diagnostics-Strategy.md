# ADR-008: Strategia Diagnostyki - Eventy i Observability

**Status:** Propozycja
**Data:** 2026-01-31
**Autor:** [Do uzupełnienia]

## Problem

Potrzebujemy spójnej strategii logowania i diagnostyki w bibliotece proxy, która:

1. **Nie wymusza zależności** - Core library nie powinien zależeć od konkretnego frameworka logowania
2. **Wspiera wiele odbiorców** - Logowanie, Application Insights, Prometheus, OpenTelemetry jednocześnie
3. **Jest wydajna** - Zero overhead gdy diagnostyka nie jest potrzebna
4. **Jest testowalna** - Łatwe mockowanie w testach jednostkowych
5. **Wspiera distributed tracing** - Propagacja Correlation ID między serwisami

**Kontekst:**

Obecnie biblioteka nie emituje żadnych zdarzeń diagnostycznych, co utrudnia:
- Debugowanie problemów w produkcji
- Monitorowanie wydajności (latency, error rates)
- Śledzenie przepływu żądań między serwisami
- Alertowanie na podstawie circuit breaker state

## Decyzja

Implementujemy **strategię opartą na eventach** z interfejsem `IProxyDiagnostics`:

### Architektura

```
┌─────────────────────────────────────────────────────────────────┐
│                    Voyager.Common.Proxy.Client                   │
│                                                                  │
│   ┌──────────────────┐         ┌──────────────────────────┐    │
│   │ HttpMethod       │────────▶│ IEnumerable<IProxy       │    │
│   │ Interceptor      │ emits   │ Diagnostics>             │    │
│   └──────────────────┘         └──────────────────────────┘    │
│                                           │                     │
│   ┌──────────────────┐                    │                     │
│   │ CircuitBreaker   │────────────────────┤                     │
│   │ Policy           │ emits              │                     │
│   └──────────────────┘                    │                     │
│                                           │                     │
└───────────────────────────────────────────┼─────────────────────┘
                                            │
              ┌─────────────────────────────┼─────────────────────────────┐
              │                             │                             │
              ▼                             ▼                             ▼
    ┌─────────────────┐          ┌─────────────────┐          ┌─────────────────┐
    │ LoggingProxy    │          │ AppInsightsProxy│          │ MetricsProxy    │
    │ Diagnostics     │          │ Diagnostics     │          │ Diagnostics     │
    │                 │          │                 │          │                 │
    │ → ILogger       │          │ → TelemetryClient│         │ → Prometheus    │
    └─────────────────┘          └─────────────────┘          └─────────────────┘
```

### Interfejs IProxyDiagnostics

Interfejs definiuje kontrakt dla DI, mockowania i testów:

```csharp
namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Interface for receiving diagnostic events from the proxy.
    /// Implement this interface to integrate with logging, APM, or metrics systems.
    /// </summary>
    /// <remarks>
    /// For easier implementation, inherit from <see cref="ProxyDiagnosticsHandler"/>
    /// which provides default empty implementations for all methods.
    /// </remarks>
    public interface IProxyDiagnostics
    {
        /// <summary>
        /// Called when an HTTP request is about to be sent.
        /// </summary>
        void OnRequestStarting(RequestStartingEvent e);

        /// <summary>
        /// Called when an HTTP request completes successfully.
        /// </summary>
        void OnRequestCompleted(RequestCompletedEvent e);

        /// <summary>
        /// Called when an HTTP request fails.
        /// </summary>
        void OnRequestFailed(RequestFailedEvent e);

        /// <summary>
        /// Called when a retry attempt is about to be made.
        /// </summary>
        void OnRetryAttempt(RetryAttemptEvent e);

        /// <summary>
        /// Called when circuit breaker state changes.
        /// </summary>
        void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e);
    }
}
```

### Klasa abstrakcyjna ProxyDiagnosticsHandler

Klasa bazowa z domyślnymi pustymi implementacjami - użytkownik nadpisuje tylko metody, które go interesują:

```csharp
namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Base class for proxy diagnostics handlers.
    /// Override only the events you're interested in - all methods have default empty implementations.
    /// </summary>
    /// <example>
    /// <code>
    /// // Only handle circuit breaker events
    /// public class SlackAlertHandler : ProxyDiagnosticsHandler
    /// {
    ///     public override void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
    ///     {
    ///         if (e.NewState == CircuitState.Open)
    ///             _slack.SendAlert($"Circuit breaker OPEN: {e.ServiceName}");
    ///     }
    ///     // Other methods use default empty implementation from base class
    /// }
    /// </code>
    /// </example>
    public abstract class ProxyDiagnosticsHandler : IProxyDiagnostics
    {
        /// <inheritdoc />
        public virtual void OnRequestStarting(RequestStartingEvent e) { }

        /// <inheritdoc />
        public virtual void OnRequestCompleted(RequestCompletedEvent e) { }

        /// <inheritdoc />
        public virtual void OnRequestFailed(RequestFailedEvent e) { }

        /// <inheritdoc />
        public virtual void OnRetryAttempt(RetryAttemptEvent e) { }

        /// <inheritdoc />
        public virtual void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e) { }
    }
}
```

### Dlaczego interfejs + klasa abstrakcyjna?

| Komponent | Użycie |
|-----------|--------|
| `IProxyDiagnostics` | DI registration, mockowanie w testach, type constraints |
| `ProxyDiagnosticsHandler` | Łatwa implementacja - nadpisz tylko to, co potrzebujesz |

```csharp
// Pełna implementacja - używa interfejsu
public class FullMetricsHandler : IProxyDiagnostics
{
    // Musi zaimplementować WSZYSTKIE metody
    public void OnRequestStarting(RequestStartingEvent e) { ... }
    public void OnRequestCompleted(RequestCompletedEvent e) { ... }
    public void OnRequestFailed(RequestFailedEvent e) { ... }
    public void OnRetryAttempt(RetryAttemptEvent e) { ... }
    public void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e) { ... }
}

// Selektywna implementacja - używa klasy abstrakcyjnej
public class CircuitBreakerAlerter : ProxyDiagnosticsHandler
{
    // Nadpisuje TYLKO to, co potrzebuje
    public override void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
    {
        if (e.NewState == CircuitState.Open)
            SendSlackAlert(e);
    }
    // Pozostałe metody - domyślne puste z klasy bazowej
}
```

### Event Records

```csharp
namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Event emitted when a request is starting.
    /// </summary>
    public sealed record RequestStartingEvent
    {
        public required string ServiceName { get; init; }
        public required string MethodName { get; init; }
        public required string HttpMethod { get; init; }
        public required string Url { get; init; }
        public required Guid CorrelationId { get; init; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Event emitted when a request completes successfully.
    /// </summary>
    public sealed record RequestCompletedEvent
    {
        public required string ServiceName { get; init; }
        public required string MethodName { get; init; }
        public required string HttpMethod { get; init; }
        public required string Url { get; init; }
        public required int StatusCode { get; init; }
        public required TimeSpan Duration { get; init; }
        public required bool IsSuccess { get; init; }
        public required Guid CorrelationId { get; init; }
        public string? ErrorType { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Event emitted when a request fails with an exception.
    /// </summary>
    public sealed record RequestFailedEvent
    {
        public required string ServiceName { get; init; }
        public required string MethodName { get; init; }
        public required string HttpMethod { get; init; }
        public required string Url { get; init; }
        public required TimeSpan Duration { get; init; }
        public required string ExceptionType { get; init; }
        public required string ExceptionMessage { get; init; }
        public required Guid CorrelationId { get; init; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Event emitted when a retry attempt is about to be made.
    /// </summary>
    public sealed record RetryAttemptEvent
    {
        public required string ServiceName { get; init; }
        public required string MethodName { get; init; }
        public required int AttemptNumber { get; init; }
        public required int MaxAttempts { get; init; }
        public required TimeSpan Delay { get; init; }
        public required string ErrorType { get; init; }
        public required string ErrorMessage { get; init; }
        public required Guid CorrelationId { get; init; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Event emitted when circuit breaker state changes.
    /// </summary>
    public sealed record CircuitBreakerStateChangedEvent
    {
        public required string ServiceName { get; init; }
        public required CircuitState OldState { get; init; }
        public required CircuitState NewState { get; init; }
        public int FailureCount { get; init; }
        public string? LastErrorType { get; init; }
        public string? LastErrorMessage { get; init; }
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }
}
```

### API rejestracji

```csharp
// Fluent API dla wielu handlerów
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddDiagnostics(d => d
        .UseLogging()                    // Wbudowany: ILogger
        .UseApplicationInsights()        // Pakiet: Voyager.Common.Proxy.Diagnostics.AppInsights
        .Use<MyCustomDiagnostics>());    // Custom handler

// Lub osobno
services.AddProxyDiagnostics<LoggingProxyDiagnostics>();
services.AddProxyDiagnostics<AppInsightsProxyDiagnostics>();
```

### Wbudowane implementacje

| Handler | Pakiet | Opis |
|---------|--------|------|
| `NullProxyDiagnostics` | Core | Domyślny, nic nie robi |
| `LoggingProxyDiagnostics` | Core | Loguje do `ILogger` |
| `AppInsightsProxyDiagnostics` | Osobny NuGet | Wysyła do Application Insights |
| `OpenTelemetryProxyDiagnostics` | Osobny NuGet | Integracja z OpenTelemetry |

### Poziomy logowania

| Event | Log Level | Kiedy |
|-------|-----------|-------|
| RequestStarting | Debug | Zawsze |
| RequestCompleted (success) | Debug | Zawsze |
| RequestCompleted (failure) | Warning | Błąd biznesowy |
| RequestFailed | Error | Exception |
| RetryAttempt | Warning | Każda próba |
| CircuitBreakerStateChanged (→Open) | Warning | Otwarcie CB |
| CircuitBreakerStateChanged (→Closed) | Information | Zamknięcie CB |

### Distributed Tracing

```csharp
public sealed record RequestStartingEvent
{
    // ...existing properties...

    /// <summary>
    /// Correlation ID for distributed tracing.
    /// Automatically propagated via Activity.Current or generated if none exists.
    /// </summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>
    /// Parent Activity ID for OpenTelemetry/W3C trace context.
    /// </summary>
    public string? ParentActivityId { get; init; }

    /// <summary>
    /// Trace ID for W3C trace context.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Span ID for W3C trace context.
    /// </summary>
    public string? SpanId { get; init; }
}
```

## Dlaczego ta opcja

### Porównanie z alternatywami

| Aspekt | ILogger bezpośrednio | DiagnosticSource | Eventy (wybrane) |
|--------|---------------------|------------------|------------------|
| Zależności | Microsoft.Extensions.Logging | System.Diagnostics | Brak |
| Wielokrotni odbiorcy | ❌ Jeden | ✅ Tak | ✅ Tak |
| Testowalność | Średnia | Trudna | ✅ Łatwa |
| Performance | Dobra | Bardzo dobra | ✅ Bardzo dobra |
| Elastyczność | Niska | Średnia | ✅ Wysoka |
| Krzywa uczenia | Niska | Wysoka | ✅ Niska |

### Zalety wybranego podejścia

1. **Zero zależności w core** - `IProxyDiagnostics` nie wymaga żadnych zewnętrznych pakietów
2. **Wielu odbiorców** - `IEnumerable<IProxyDiagnostics>` pozwala na równoległe przetwarzanie
3. **Testowalność** - Łatwe mockowanie interfejsu w testach
4. **Wydajność** - Gdy brak handlerów, overhead to tylko sprawdzenie pustej kolekcji
5. **Elastyczność** - Użytkownik decyduje co robić z eventami
6. **SOLID** - Open/Closed principle - rozszerzanie bez modyfikacji core

### Wady i mitigacje

| Wada | Mitigacja |
|------|-----------|
| Nowy interfejs do nauki | Dobra dokumentacja, wbudowane implementacje |
| Potencjalny overhead przy wielu handlerach | Handler może być async, fire-and-forget |
| Brak integracji z istniejącymi narzędziami APM | Dostarczamy gotowe pakiety (AppInsights, OpenTelemetry) |

## Alternatywy które odrzuciliśmy

### Alternatywa 1: Bezpośrednie użycie ILogger

```csharp
public class HttpMethodInterceptor
{
    private readonly ILogger<HttpMethodInterceptor> _logger;

    public async Task<object> InterceptAsync(...)
    {
        _logger.LogDebug("Request starting: {Method} {Url}", method, url);
        // ...
    }
}
```

**Dlaczego odrzucona:**
- Wymusza zależność od `Microsoft.Extensions.Logging` w core
- Jeden odbiorca (tylko logger)
- Trudniejsze dodanie metryk, APM bez modyfikacji kodu

### Alternatywa 2: DiagnosticSource

```csharp
private static readonly DiagnosticSource _diagnosticSource =
    new DiagnosticListener("Voyager.Common.Proxy");

if (_diagnosticSource.IsEnabled("RequestStarting"))
{
    _diagnosticSource.Write("RequestStarting", new { Method = method, Url = url });
}
```

**Dlaczego odrzucona:**
- Skomplikowane API (Write, IsEnabled, Subscribe)
- Trudniejsze w testach jednostkowych
- Mniej intuicyjne dla użytkowników

### Alternatywa 3: EventSource (ETW)

```csharp
[EventSource(Name = "Voyager-Common-Proxy")]
public sealed class ProxyEventSource : EventSource
{
    [Event(1, Level = EventLevel.Informational)]
    public void RequestStarting(string method, string url) { ... }
}
```

**Dlaczego odrzucona:**
- Głównie dla Windows ETW
- Wymaga narzędzi ETW do analizy
- Mniej elastyczne niż eventy

## Punkty emisji zdarzeń

### Kto wywołuje handlery?

`HttpMethodInterceptor` jest odpowiedzialny za emitowanie wszystkich zdarzeń diagnostycznych:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        HttpMethodInterceptor                             │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  InterceptAsync()                                                        │
│       │                                                                  │
│       ▼                                                                  │
│  ExecuteWithResilienceAsync()                                            │
│       │                                                                  │
│       ├──▶ _circuitBreaker.ShouldAllowRequestAsync()                    │
│       │         │                                                        │
│       │         └──▶ [Stan CB się zmienił?] ──▶ OnCircuitBreakerStateChanged │
│       │                                                                  │
│       ▼                                                                  │
│  ExecuteWithRetryAsync()  ◀─────────────────────────────┐               │
│       │                                                  │               │
│       ▼                                                  │               │
│  ExecuteHttpRequestAsync()                               │               │
│       │                                                  │               │
│       ├──▶ OnRequestStarting ────────────────────────────┤               │
│       │                                                  │               │
│       ▼                                                  │               │
│  [HTTP Call]                                             │               │
│       │                                                  │               │
│       ├── Success ──▶ OnRequestCompleted ────────────────┤               │
│       │                                                  │               │
│       └── Failure ──▶ OnRequestFailed ───────────────────┤               │
│                       │                                  │               │
│                       ├── IsTransient? ──▶ OnRetryAttempt│               │
│                       │                      + delay     │               │
│                       │                          │       │               │
│                       │                          └───────┘  (retry loop) │
│                       │                                                  │
│                       └──▶ RecordResultForCircuitBreakerAsync()         │
│                                   │                                      │
│                                   └──▶ [Stan CB się zmienił?]           │
│                                              ──▶ OnCircuitBreakerStateChanged │
│                                                                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Szczegóły emisji zdarzeń

| Zdarzenie | Miejsce emisji | Kod |
|-----------|----------------|-----|
| `OnRequestStarting` | `ExecuteHttpRequestAsync()` - przed `_httpClient.SendAsync()` | Linia ~225 |
| `OnRequestCompleted` | `ExecuteHttpRequestAsync()` - po `ResultMapper.MapResponseAsync()` | Linia ~242 |
| `OnRequestFailed` | `ExecuteHttpRequestAsync()` - w bloku `catch` | Linie 243-258 |
| `OnRetryAttempt` | Callback `onRetryAttempt` w `BindWithRetryAsync()` | Voyager.Common.Results 1.7.0-preview.2 |
| `OnCircuitBreakerStateChanged` | Callback `OnStateChanged` w `CircuitBreakerPolicy` | Voyager.Common.Resilience 1.7.0-preview.2 |

### Rozwiązanie: Circuit Breaker State Changes

Od wersji **Voyager.Common.Resilience 1.7.0-preview.2** dostępny jest callback `OnStateChanged`:

```csharp
_circuitBreaker.OnStateChanged = (oldState, newState, failures, lastError) =>
{
    foreach (var handler in _diagnostics)
        handler.OnCircuitBreakerStateChanged(new CircuitBreakerStateChangedEvent
        {
            ServiceName = _serviceName,
            OldState = oldState,
            NewState = newState,
            FailureCount = failures,
            LastErrorType = lastError?.Type.ToString(),
            LastErrorMessage = lastError?.Message
        });
};
```

**Alternatywne rozwiązanie: ObservableCircuitBreaker wrapper**

Dla starszych wersji biblioteki można użyć wrappera:

```csharp
namespace Voyager.Common.Proxy.Client.Internal
{
    /// <summary>
    /// Wrapper around CircuitBreakerPolicy that tracks state changes
    /// and emits diagnostic events.
    /// </summary>
    internal sealed class ObservableCircuitBreaker
    {
        private readonly CircuitBreakerPolicy _policy;
        private readonly IEnumerable<IProxyDiagnostics> _diagnostics;
        private readonly string _serviceName;
        private CircuitState _lastKnownState;

        public ObservableCircuitBreaker(
            CircuitBreakerPolicy policy,
            IEnumerable<IProxyDiagnostics> diagnostics,
            string serviceName)
        {
            _policy = policy;
            _diagnostics = diagnostics;
            _serviceName = serviceName;
            _lastKnownState = policy.State;
        }

        public async Task<Result<bool>> ShouldAllowRequestAsync()
        {
            var oldState = _policy.State;
            var result = await _policy.ShouldAllowRequestAsync().ConfigureAwait(false);
            CheckStateChange(oldState);
            return result;
        }

        public async Task RecordSuccessAsync()
        {
            var oldState = _policy.State;
            await _policy.RecordSuccessAsync().ConfigureAwait(false);
            CheckStateChange(oldState);
        }

        public async Task RecordFailureAsync(Error error)
        {
            var oldState = _policy.State;
            await _policy.RecordFailureAsync(error).ConfigureAwait(false);
            CheckStateChange(oldState);
        }

        private void CheckStateChange(CircuitState oldState)
        {
            var newState = _policy.State;
            if (oldState != newState)
            {
                var evt = new CircuitBreakerStateChangedEvent
                {
                    ServiceName = _serviceName,
                    OldState = oldState,
                    NewState = newState,
                    FailureCount = _policy.FailureCount,
                    LastErrorType = _policy.LastError?.Type.ToString(),
                    LastErrorMessage = _policy.LastError?.Message
                };

                foreach (var handler in _diagnostics)
                {
                    try
                    {
                        handler.OnCircuitBreakerStateChanged(evt);
                    }
                    catch
                    {
                        // Diagnostics should never break the main flow
                    }
                }
            }
        }

        public CircuitState State => _policy.State;
    }
}
```

### Rozwiązanie: Retry Attempt Callbacks

Od wersji **Voyager.Common.Results 1.7.0-preview.2** dostępny jest callback `onRetryAttempt` w `BindWithRetryAsync`:

- [ADR-0009: Retry Attempt Callbacks](file:///C:/src/Voyager.Common.Results/docs/adr/ADR-0009-retry-attempt-callbacks.md)

Użycie:

```csharp
var result = await operation.BindWithRetryAsync(
    async _ => await ExecuteHttpRequestAsync(...),
    _retryPolicy,
    onRetryAttempt: (attempt, error, delayMs) =>
    {
        foreach (var handler in _diagnostics)
            handler.OnRetryAttempt(new RetryAttemptEvent
            {
                ServiceName = _serviceName,
                MethodName = methodName,
                AttemptNumber = attempt,
                ErrorType = error.Type.ToString(),
                ErrorMessage = error.Message,
                Delay = TimeSpan.FromMilliseconds(delayMs),
                // ...
            });
    });
```

### Zmiany w HttpMethodInterceptor

```csharp
internal sealed class HttpMethodInterceptor : IMethodInterceptor
{
    private readonly IEnumerable<IProxyDiagnostics> _diagnostics;
    private readonly ObservableCircuitBreaker? _circuitBreaker;  // Zamiast CircuitBreakerPolicy
    private readonly string _serviceName;

    // W ExecuteWithRetryAsync, przed Task.Delay:
    private async Task<object> ExecuteWithRetryAsync(...)
    {
        // ... existing code ...

        // Przed retry - emit event
        var retryEvent = new RetryAttemptEvent
        {
            ServiceName = _serviceName,
            MethodName = method.Name,
            AttemptNumber = attempt,
            MaxAttempts = maxAttempts,
            Delay = TimeSpan.FromMilliseconds(delayMs),
            ErrorType = error.Type.ToString(),
            ErrorMessage = error.Message,
            CorrelationId = GetCorrelationId()
        };

        EmitEvent(d => d.OnRetryAttempt(retryEvent));

        await Task.Delay(delayMs).ConfigureAwait(false);
    }

    // W ExecuteHttpRequestAsync:
    private async Task<object> ExecuteHttpRequestAsync(...)
    {
        var correlationId = GetCorrelationId();
        var startTime = Stopwatch.GetTimestamp();

        // Emit: Request Starting
        EmitEvent(d => d.OnRequestStarting(new RequestStartingEvent
        {
            ServiceName = _serviceName,
            MethodName = method.Name,
            HttpMethod = httpMethod.ToString(),
            Url = path,
            CorrelationId = correlationId
        }));

        try
        {
            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            var result = await ResultMapper.MapResponseAsync(...).ConfigureAwait(false);
            var duration = GetElapsed(startTime);

            // Emit: Request Completed
            EmitEvent(d => d.OnRequestCompleted(new RequestCompletedEvent
            {
                ServiceName = _serviceName,
                MethodName = method.Name,
                HttpMethod = httpMethod.ToString(),
                Url = path,
                StatusCode = (int)response.StatusCode,
                Duration = duration,
                IsSuccess = IsSuccessResult(result, resultType),
                CorrelationId = correlationId,
                ErrorType = GetErrorFromResult(result, resultType)?.Type.ToString(),
                ErrorMessage = GetErrorFromResult(result, resultType)?.Message
            }));

            return result;
        }
        catch (Exception ex)
        {
            var duration = GetElapsed(startTime);

            // Emit: Request Failed
            EmitEvent(d => d.OnRequestFailed(new RequestFailedEvent
            {
                ServiceName = _serviceName,
                MethodName = method.Name,
                HttpMethod = httpMethod.ToString(),
                Url = path,
                Duration = duration,
                ExceptionType = ex.GetType().FullName!,
                ExceptionMessage = ex.Message,
                CorrelationId = correlationId
            }));

            // ... existing exception handling ...
        }
    }

    private void EmitEvent(Action<IProxyDiagnostics> action)
    {
        foreach (var handler in _diagnostics)
        {
            try
            {
                action(handler);
            }
            catch
            {
                // Diagnostics should never break the main flow
            }
        }
    }

    private static Guid GetCorrelationId()
    {
        // Use Activity if available (OpenTelemetry), otherwise generate new
        if (Activity.Current != null)
        {
            return Guid.TryParse(Activity.Current.TraceId.ToString(), out var traceId)
                ? traceId
                : Guid.NewGuid();
        }
        return Guid.NewGuid();
    }
}
```

## Implementacja

### Faza 1: Core Events

- [ ] Utworzenie `Voyager.Common.Proxy.Diagnostics` namespace
- [ ] Definicja `IProxyDiagnostics` interfejsu
- [ ] Definicja `ProxyDiagnosticsHandler` klasy abstrakcyjnej
- [ ] Definicja event records
- [ ] `NullProxyDiagnostics` (domyślny, singleton)
- [ ] `ObservableCircuitBreaker` wrapper
- [ ] Integracja z `HttpMethodInterceptor` - emisja zdarzeń
- [ ] Przekazywanie `IEnumerable<IProxyDiagnostics>` przez DI

### Faza 2: Wbudowane Handlery

- [ ] `LoggingProxyDiagnostics` (ILogger)
- [ ] Extension methods dla rejestracji
- [ ] Testy jednostkowe
- [ ] Dokumentacja w README

### Faza 3: Zewnętrzne Pakiety (opcjonalne)

- [ ] `Voyager.Common.Proxy.Diagnostics.AppInsights`
- [ ] `Voyager.Common.Proxy.Diagnostics.OpenTelemetry`
- [ ] Przykłady integracji

## Przykłady użycia

### Podstawowe logowanie

```csharp
// Rejestracja
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddDiagnostics(d => d.UseLogging());

// Wynik w logach:
// [DBG] Voyager.Proxy: Request starting: GET https://api.example.com/users/123 [correlation: abc-123]
// [DBG] Voyager.Proxy: Request completed: GET https://api.example.com/users/123 200 OK (45ms) [correlation: abc-123]
```

### Application Insights

```csharp
// Rejestracja
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddDiagnostics(d => d
        .UseLogging()
        .UseApplicationInsights());

// Wynik w App Insights:
// - Dependency tracking z correlation
// - Custom metrics (request count, latency)
// - Circuit breaker events jako custom events
```

### Custom handler

```csharp
public class SlackAlertingDiagnostics : IProxyDiagnostics
{
    private readonly ISlackClient _slack;

    public void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
    {
        if (e.NewState == CircuitState.Open)
        {
            _slack.SendAlert($"🔴 Circuit breaker OPEN for {e.ServiceName}: {e.LastErrorMessage}");
        }
    }

    // Inne metody - puste implementacje
    public void OnRequestStarting(RequestStartingEvent e) { }
    public void OnRequestCompleted(RequestCompletedEvent e) { }
    public void OnRequestFailed(RequestFailedEvent e) { }
    public void OnRetryAttempt(RetryAttemptEvent e) { }
}

// Rejestracja
services.AddProxyDiagnostics<SlackAlertingDiagnostics>();
```

## Metryki sukcesu

- Użytkownicy mogą logować żądania bez modyfikacji kodu biblioteki
- Wsparcie dla Application Insights działa "out of the box"
- Zero overhead gdy diagnostyka wyłączona
- Correlation ID propagowany między serwisami

---

**Powiązane dokumenty:**
- [ADR-007: Resilience Strategy](./ADR-007-Resilience-Strategy.md)
- [ADR-009: Upgrade do Results 1.7.0](./ADR-009-Upgrade-Results-1.7.0.md)
- [Microsoft.Extensions.Logging](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging)
- [Application Insights](https://docs.microsoft.com/en-us/azure/azure-monitor/app/app-insights-overview)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)

**Zależności od Voyager.Common.Results/Resilience (wersja 1.7.0-preview.2):**
- [ADR-0008: Circuit Breaker State Change Callbacks](file:///C:/src/Voyager.Common.Results/docs/adr/ADR-0008-circuit-breaker-state-change-callbacks.md) - zaimplementowane
- [ADR-0009: Retry Attempt Callbacks](file:///C:/src/Voyager.Common.Results/docs/adr/ADR-0009-retry-attempt-callbacks.md) - zaimplementowane
