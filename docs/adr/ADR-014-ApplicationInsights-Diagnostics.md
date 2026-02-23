# ADR-014: Integracja Diagnostyki z Application Insights

**Status:** Zaimplementowano
**Data:** 2026-02-18

## Problem

Biblioteka proxy posiada kompletną infrastrukturę diagnostyczną (ADR-008: eventy, interfejs `IProxyDiagnostics`, emittery), ale brakuje gotowej integracji z Azure Application Insights - najczęściej używanym narzędziem APM w ekosystemie produktów Voyager.

Każdy zespół implementujący proxy musiałby samodzielnie mapować eventy diagnostyczne na telemetrię AI, co prowadzi do:

- **Niespójności** - różne zespoły mapują eventy na różne typy telemetrii
- **Duplikacji kodu** - każdy projekt powtarza tę samą logikę
- **Braków** - łatwo pominąć trace context lub custom properties

## Decyzja

Tworzymy nowy pakiet **Voyager.Common.Proxy.Diagnostics.ApplicationInsights** zawierający implementację `ApplicationInsightsProxyDiagnostics`.

### Mapowanie eventów na telemetrię

| Event | Telemetria AI | Nazwa / Typ |
|-------|--------------|-------------|
| `OnRequestCompleted` | `DependencyTelemetry` | type=`VoyagerProxy`, success/failure, duration, resultCode |
| `OnRequestFailed` | `ExceptionTelemetry` + `DependencyTelemetry` | Severity=Error + failed dependency |
| `OnRetryAttempt` | `EventTelemetry` | `ProxyRetryAttempt` |
| `OnCircuitBreakerStateChanged` | `EventTelemetry` | `ProxyCircuitBreakerStateChanged` |
| `OnRequestStarting` | *(no-op)* | Pokryte przez DependencyTelemetry w Completed/Failed |

### Mapowanie właściwości → customDimensions

Wszystkie elementy telemetrii zawierają następujące właściwości (gdzie dostępne):

| Właściwość AI (customDimensions) | Źródło |
|---|---|
| `ServiceName` | Nazwa interfejsu serwisu |
| `MethodName` | Nazwa wywoływanej metody |
| `HttpMethod` | Metoda HTTP (GET, POST, ...) |
| `Url` | URL żądania (ścieżka względna) |
| `UserLogin` | Login użytkownika |
| `UnitId` | Identyfikator jednostki organizacyjnej |
| `UnitType` | Typ jednostki organizacyjnej |
| *(custom)* | Wszystkie wpisy z `IProxyRequestContext.CustomProperties` |

### W3C Trace Context

Na wszystkich elementach telemetrii ustawiane są:
- `operation_Id` = TraceId (32 hex)
- `operation_ParentId` = SpanId (16 hex)

### Separacja środowisk

Strategia dwupoziomowa:

1. **Connection String** - per środowisko (DEV/STG/PROD) wskazuje na osobny zasób Application Insights
2. **Cloud.RoleName** - konfigurowalny przez `ApplicationInsightsOptions.CloudRoleName`, pozwala rozróżniać serwisy w Application Map

```csharp
services.AddProxyApplicationInsightsDiagnostics(options =>
{
    options.CloudRoleName = "MyService-Production";
});
```

### Bezpieczeństwo

Handler nigdy nie rzuca wyjątków - wszystkie metody opatrzone są blokiem `try/catch`, aby diagnostyka nie wpływała na działanie głównej logiki proxy.

## Alternatywy

### 1. OpenTelemetry zamiast natywnego AI SDK

**Odrzucone**, ponieważ:
- Wiele istniejących projektów Voyager używa bezpośrednio `Microsoft.ApplicationInsights`
- OpenTelemetry wymaga dodatkowej konfiguracji eksportera
- Pakiet może współistnieć z przyszłą integracją OpenTelemetry

### 2. Użycie `ITelemetryInitializer` zamiast bezpośredniego `TrackDependency`

**Odrzucone**, ponieważ:
- Initializer modyfikuje istniejącą telemetrię, a nie tworzy nową
- Potrzebujemy jawnego mapowania event → konkretny typ telemetrii
- Approach z `TelemetryClient` jest prostszy i bardziej testowalny

## Struktura pakietu

```
Voyager.Common.Proxy.Diagnostics.ApplicationInsights/
├── ApplicationInsightsProxyDiagnostics.cs    // IProxyDiagnostics → TelemetryClient
├── ApplicationInsightsOptions.cs             // CloudRoleName config
├── ApplicationInsightsDiagnosticsExtensions.cs  // DI registration
└── README.md
```

## Konsekwencje

- **Pozytywne**: Gotowa do użycia integracja AI bez konieczności pisania boilerplate
- **Pozytywne**: Spójne mapowanie eventów na telemetrię we wszystkich projektach
- **Pozytywne**: Pełna korelacja z distributed traces (W3C)
- **Negatywne**: Dodatkowa zależność na `Microsoft.ApplicationInsights` (~2.22.0)
- **Neutralne**: Nie wyklucza przyszłej integracji z OpenTelemetry (mogą współistnieć)
