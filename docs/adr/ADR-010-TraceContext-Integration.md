# ADR-010: Integracja z Voyager.TraceContext

**Status:** Zaimplementowane (Faza 1 i Faza 2 ukończone)
**Data:** 2026-02-02
**Autor:** [Do uzupełnienia]

## Problem

Obecna implementacja diagnostyki w `Voyager.Common.Proxy` ma ograniczone wsparcie dla distributed tracing:

1. **Brak pełnego W3C Trace Context** - brak pełnego trace/span/parent hierarchy
2. **Brak child spans** - wszystkie operacje w ramach request mają ten sam ID
3. **Duplikacja logiki** - `DiagnosticsEmitter.GetCorrelationId()` reimplementuje logikę dostępu do `Activity.Current`
4. **Brak integracji z logowaniem** - trace context nie jest automatycznie dodawany do wszystkich logów

W organizacji istnieje już biblioteka **Voyager.TraceContext** która:
- Implementuje pełny W3C Trace Context (traceparent, tracestate)
- Wspiera .NET Framework 4.8 i .NET 8+
- Integruje się z Serilog i Enterprise Library
- Ma 275 testów i jest używana w produkcji

## Decyzja

Tworzymy nowy pakiet **`Voyager.Common.Proxy.Diagnostics.TraceContext`** który integruje bibliotekę TraceContext z diagnostyką proxy.

### Architektura integracji

```
┌─────────────────────────────────────────────────────────────────────────────────┐
│                    Voyager.Common.Proxy.Abstractions                             │
│                                                                                  │
│  Diagnostics/Events/                                                             │
│  ├── RequestStartingEvent      ──┬── TraceId (string)     - W3C 32 hex          │
│  ├── RequestCompletedEvent       ├── SpanId (string)      - W3C 16 hex          │
│  ├── RequestFailedEvent          └── ParentSpanId (string?) - W3C 16 hex        │
│  └── RetryAttemptEvent                                                          │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        │ uses
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                 Voyager.Common.Proxy.Diagnostics.TraceContext                    │
│                              (NOWY PAKIET)                                       │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│  TraceContextDiagnosticsEmitter.cs                                              │
│    └── Używa ITraceContextAccessor do pobierania trace context                  │
│                                                                                  │
│  TraceContextProxyDiagnostics.cs                                                │
│    └── Handler wzbogacający eventy o trace context                              │
│                                                                                  │
│  ServiceCollectionExtensions.cs                                                  │
│    └── AddProxyTraceContextDiagnostics()                                        │
│                                                                                  │
│  Zależności:                                                                     │
│  - Voyager.Common.Proxy.Abstractions                                            │
│  - Voyager.TraceContext.Abstractions                                            │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
                                        │
                                        │ references
                                        ▼
┌─────────────────────────────────────────────────────────────────────────────────┐
│                      Voyager.TraceContext.Abstractions                           │
│                        (zewnętrzny pakiet NuGet)                                 │
├─────────────────────────────────────────────────────────────────────────────────┤
│                                                                                  │
│  ITraceContextAccessor                                                           │
│    ├── TraceId       (string) - 32 hex chars                                    │
│    ├── SpanId        (string) - 16 hex chars                                    │
│    ├── ParentSpanId  (string?) - 16 hex chars                                   │
│    ├── TraceFlags    (string) - 2 hex chars                                     │
│    └── TraceState    (string?) - vendor state                                   │
│                                                                                  │
│  ITraceContext (extended, read/write)                                           │
│    └── Set(), Clear(), GetPropagationHeaders()                                  │
│                                                                                  │
└─────────────────────────────────────────────────────────────────────────────────┘
```

### Zmiany w polach eventów diagnostycznych

#### Pola W3C Trace Context

| Pole | Typ | Opis | Źródło |
|------|-----|------|--------|
| `TraceId` | `string` | 32-znakowy hex ID śledzenia | `ITraceContextAccessor.TraceId` |
| `SpanId` | `string` | 16-znakowy hex ID aktualnego span | `ITraceContextAccessor.SpanId` |
| `ParentSpanId` | `string?` | 16-znakowy hex ID rodzica span (null dla root spans) | `ITraceContextAccessor.ParentSpanId` |

#### Pola które USUWAMY

| Pole | Powód usunięcia |
|------|-----------------|
| `ParentActivityId` | Zastąpione przez `ParentSpanId` - W3C standard zamiast Activity-specific |

### Zaktualizowana struktura eventów

```csharp
public sealed class RequestStartingEvent
{
    // === Identyfikacja żądania ===
    public required string ServiceName { get; init; }
    public required string MethodName { get; init; }
    public required string HttpMethod { get; init; }
    public required string Url { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    // === Distributed Tracing (W3C Trace Context) ===
    /// <summary>
    /// W3C Trace ID (32 hex characters).
    /// </summary>
    public required string TraceId { get; init; }

    /// <summary>
    /// W3C Span ID (16 hex characters) - unique per operation.
    /// </summary>
    public required string SpanId { get; init; }

    /// <summary>
    /// W3C Parent Span ID (16 hex characters).
    /// Null for root spans.
    /// </summary>
    public string? ParentSpanId { get; init; }

    // === Kontekst użytkownika ===
    public string? UserLogin { get; init; }
    public string? UnitId { get; init; }
    public string? UnitType { get; init; }
    public IReadOnlyDictionary<string, string>? CustomProperties { get; init; }
}
```

### Jak działa integracja

#### 1. Pobieranie trace context

```csharp
// W DiagnosticsEmitter (po integracji):
public static class TraceContextDiagnosticsEmitter
{
    public static (string traceId, string spanId, string? parentSpanId)
        GetTraceContext(ITraceContextAccessor accessor)
    {
        return (accessor.TraceId, accessor.SpanId, accessor.ParentSpanId);
    }
}
```

#### 2. Rejestracja w DI

```csharp
// Program.cs / Startup.cs
services.AddTraceContext(configuration);  // Z Voyager.TraceContext.Core

services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddDiagnostics(d => d
        .UseTraceContext()   // NOWE - integracja z TraceContext
        .UseLogging());
```

#### 3. OWIN (bez DI)

```csharp
// Startup.cs (OWIN)
public void Configuration(IAppBuilder app)
{
    // TraceContext middleware (z Voyager.TraceContext.Framework)
    app.Use<TraceContextMiddleware>();

    // Proxy z diagnostyką
    app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
    {
        options.ServiceFactory = () => container.Resolve<IVIPService>();

        // Używa FrameworkTraceContextAccessor (z TraceContext.Framework)
        var accessor = new FrameworkTraceContextAccessor();
        options.TraceContextAccessor = accessor;

        options.DiagnosticsHandlers = new IProxyDiagnostics[]
        {
            new LoggingProxyDiagnostics(logger)
        };
    }));
}
```

### Mapowanie pól: Proxy Diagnostics ↔ TraceContext

| Proxy Diagnostics | TraceContext | Opis |
|-------------------|--------------|------|
| `TraceId` (string) | `ITraceContextAccessor.TraceId` | W3C 32 hex |
| `SpanId` (string) | `ITraceContextAccessor.SpanId` | W3C 16 hex |
| `ParentSpanId` (string?) | `ITraceContextAccessor.ParentSpanId` | W3C 16 hex |
| ~~`ParentActivityId`~~ | (usunięte) | Zastąpione przez ParentSpanId |

### Korzyści z integracji

| Aspekt | Opis |
|--------|------|
| **Trace hierarchy** | Hierarchiczny (trace → span → child span) |
| **Standard** | W3C Trace Context |
| **Log correlation** | Wszystkie logi (via SerilogTraceContextEnricher) |
| **Cross-service** | Automatyczna propagacja via traceparent header |
| **Kibana queries** | `trace.id: "abc123..." AND span.id: "def456..."` |

### Struktura pakietu

```
src/Voyager.Common.Proxy.Diagnostics.TraceContext/
├── Voyager.Common.Proxy.Diagnostics.TraceContext.csproj
├── ITraceContextAccessor.cs                  // Lokalny interface dla trace context
├── TraceContextHelper.cs                     // Pobiera trace context z accessor
├── TraceContextProxyRequestContext.cs        // Wrapper IProxyRequestContext z trace info
├── ServiceCollectionExtensions.cs            // AddProxyTraceContext() dla ASP.NET Core
├── OwinTraceContextExtensions.cs             // CreateRequestContextFactory() dla OWIN
└── README.md
```

### Zależności NuGet

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Description>TraceContext integration for Voyager.Common.Proxy diagnostics</Description>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Voyager.Common.Proxy.Abstractions\..." />
  </ItemGroup>

  <!-- Pakiet definiuje własny ITraceContextAccessor interface -->
  <!-- Użytkownicy mogą zaimplementować go samodzielnie lub zintegrować z zewnętrznym tracing -->
</Project>
```

**Uwaga:** Pakiet definiuje własny `ITraceContextAccessor` interface zamiast zależności od zewnętrznego pakietu.
Użytkownicy implementują ten interface, aby dostarczyć trace context z ich infrastruktury (np. OpenTelemetry, Activity.Current).

## Dlaczego ta decyzja

### Porównanie: osobny pakiet vs wbudowanie w core

| Aspekt | Wbudowane w core | Osobny pakiet (wybrane) |
|--------|------------------|-------------------------|
| Zależności core | +1 (TraceContext.Abstractions) | Brak zmian |
| Opt-in | Zawsze włączone | Dodaj pakiet gdy potrzebne |
| Breaking change | Tak (nowe zależności) | Nie |
| Użytkownicy bez TraceContext | Niepotrzebna zależność | Nie dotyczy |

### Dlaczego nie zastępujemy całej diagnostyki?

Biblioteki są **komplementarne**, nie konkurencyjne:

- **TraceContext** = propagacja kontekstu, spans, enrichment logów
- **Proxy Diagnostics** = eventy lifecycle (start, complete, fail, retry, circuit breaker)

TraceContext nie oferuje:
- Event-driven architecture dla proxy
- User context (UserLogin, UnitId, UnitType)
- Retry/Circuit Breaker specific events
- Multiple handler pattern

## Migracja

### Dla użytkowników obecnej diagnostyki

```csharp
// PRZED (bez TraceContext)
services.AddServiceProxy<IUserService>("...")
    .AddDiagnostics(d => d.UseLogging());

// PO (z TraceContext) - opcjonalne
services.AddTraceContext(configuration);  // Dodaj TraceContext
services.AddServiceProxy<IUserService>("...")
    .AddDiagnostics(d => d
        .UseTraceContext()  // Dodaj integrację
        .UseLogging());
```

### Breaking changes

| Zmiana | Wpływ | Mitigacja |
|--------|-------|-----------|
| Dodanie `TraceId`, `SpanId`, `ParentSpanId` | Nowe wymagane pola w eventach | Nowa funkcjonalność |
| Usunięcie `ParentActivityId` | Potencjalny breaking change | Zastąpione przez `ParentSpanId` |

## Implementacja

### Faza 1: Zmiany w Abstractions ✅ UKOŃCZONE

- [x] Dodać pola `TraceId`, `SpanId`, `ParentSpanId` do wszystkich eventów
- [x] Usunąć `CorrelationId` (zastąpione przez TraceId/SpanId/ParentSpanId)
- [x] Zaktualizować dokumentację pól
- [x] Zaktualizować `DiagnosticsEmitter` i `ServerDiagnosticsEmitter` z `GetTraceContext()`
- [x] Zaktualizować `LoggingProxyDiagnostics` z nowymi polami

### Faza 2: Nowy pakiet Diagnostics.TraceContext ✅ UKOŃCZONE

- [x] Utworzyć projekt `Voyager.Common.Proxy.Diagnostics.TraceContext`
- [x] Zdefiniować lokalny `ITraceContextAccessor` interface
- [x] Implementacja `TraceContextProxyRequestContext` wzbogacającego o trace context
- [x] Extension method `AddProxyTraceContext()` dla ASP.NET Core
- [x] `OwinTraceContextExtensions` dla .NET Framework
- [x] README.md z przykładami użycia
- [x] Dodanie do solution i CI/CD pipeline

### Faza 3: Integracja z OWIN ✅ UKOŃCZONE

- [x] OWIN helper - `CreateRequestContextFactory()` w OwinTraceContextExtensions

### Faza 4: Dokumentacja i przykłady

- [x] Aktualizacja README diagnostyki
- [ ] Przykłady integracji z Serilog
- [ ] Przykłady dla OWIN

## Przykłady użycia

### ASP.NET Core z pełną integracją

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// TraceContext
builder.Services.AddTraceContext(builder.Configuration);

// Serilog z TraceContext enrichment
builder.Host.UseSerilog((context, config) => config
    .Enrich.With<SerilogTraceContextEnricher>()  // Wszystkie logi mają trace.id/span.id
    .WriteTo.Console());

// Proxy z pełną diagnostyką
builder.Services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddDiagnostics(d => d
        .UseTraceContext()
        .UseLogging());

var app = builder.Build();
app.UseTraceContext();  // Middleware
app.MapServiceProxy<IUserService>();
app.Run();
```

### Wynik w logach (Kibana)

```json
{
  "@timestamp": "2026-02-02T10:30:45.123Z",
  "message": "Proxy request completed: GET /users/123 [IUserService.GetUserAsync]",
  "trace": {
    "id": "abc123def456789012345678901234ab",
    "span": {
      "id": "1234567890abcdef"
    },
    "parent": {
      "id": "fedcba0987654321"
    }
  },
  "proxy": {
    "service": "IUserService",
    "method": "GetUserAsync",
    "duration_ms": 45,
    "status_code": 200,
    "success": true
  },
  "user": {
    "login": "jan.kowalski",
    "unit_id": "12345",
    "unit_type": "Agent"
  }
}
```

### Zapytanie w Kibana

```
# Znajdź wszystkie operacje w ramach jednego trace
trace.id: "abc123def456789012345678901234ab"

# Znajdź konkretny span i jego dzieci
trace.id: "abc123..." AND trace.parent.id: "1234567890abcdef"

# Żądania proxy per użytkownik
proxy.service: "IUserService" AND user.login: "jan.kowalski"
```

## Powiązane dokumenty

- [ADR-008: Diagnostics Strategy](./ADR-008-Diagnostics-Strategy.md)
- [Voyager.TraceContext README](https://github.com/Voyager-Poland/traceContext/README.md)
- [W3C Trace Context Specification](https://www.w3.org/TR/trace-context/)
