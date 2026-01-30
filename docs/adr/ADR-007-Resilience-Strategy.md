# ADR-007: Strategia Resilience - Retry i Circuit Breaker

**Status:** Zaakceptowane
**Data:** 2026-01-30
**Autor:** [Do uzupełnienia]

## Problem

Potrzebujemy spójnej strategii obsługi błędów przejściowych (transient failures) w komunikacji HTTP między serwisami. Obecne rozwiązanie pozostawia konfigurację resilience użytkownikowi, co prowadzi do:

1. **Niespójności** - różne projekty implementują retry/circuit breaker na różne sposoby
2. **Duplikacji** - każdy projekt konfiguruje Polly od zera
3. **Błędów** - retry na błędach permanentnych (np. 404, 400) marnuje zasoby
4. **Braku integracji** - Polly działa na poziomie HTTP, nie zna semantyki `Result<T>`

**Kontekst:**

Biblioteka `Voyager.Common.Results` definiuje typy błędów z jasną semantyką:

| ErrorType | Znaczenie | Retry? | Circuit Breaker? |
|-----------|-----------|--------|------------------|
| `Unavailable` | Serwis chwilowo niedostępny | ✅ TAK | ✅ Liczy |
| `Timeout` | Przekroczony czas operacji | ✅ TAK | ✅ Liczy |
| `Database` | Błąd bazy danych | ❌ NIE | ✅ Liczy |
| `Unexpected` | Nieoczekiwany błąd systemowy | ❌ NIE | ✅ Liczy |
| `Validation` | Błędne dane wejściowe | ❌ NIE | ❌ Ignoruje |
| `NotFound` | Zasób nie istnieje | ❌ NIE | ❌ Ignoruje |
| `Permission` | Brak uprawnień | ❌ NIE | ❌ Ignoruje |
| `Unauthorized` | Brak uwierzytelnienia | ❌ NIE | ❌ Ignoruje |
| `Conflict` | Konflikt (np. duplikat) | ❌ NIE | ❌ Ignoruje |
| `Business` | Błąd logiki biznesowej | ❌ NIE | ❌ Ignoruje |
| `Cancelled` | Operacja anulowana | ❌ NIE | ❌ Ignoruje |

Biblioteka `Voyager.Common.Resilience` już implementuje te reguły:
- `RetryPolicies.TransientErrors()` - retry tylko dla `Unavailable`, `Timeout`
- `CircuitBreakerPolicy` - liczy tylko błędy infrastrukturalne

## Decyzja

Implementujemy **hybrydową strategię resilience** działającą na dwóch poziomach:

### Poziom 1: HTTP (przed mapowaniem na Result)

Obsługuje błędy transportowe, które nie docierają do warstwy aplikacji:

```
┌─────────────────────────────────────────────────────────────┐
│                    HTTP Level (Polly)                        │
├─────────────────────────────────────────────────────────────┤
│  Obsługuje:                                                  │
│  • Connection refused / reset                                │
│  • DNS resolution failure                                    │
│  • TLS/SSL handshake errors                                  │
│  • HTTP 502, 503, 504 (Gateway errors)                       │
│  • Request timeout (HttpClient.Timeout)                      │
│                                                              │
│  NIE obsługuje (przekazuje dalej):                          │
│  • HTTP 400, 401, 403, 404, 409 (błędy aplikacyjne)         │
│  • HTTP 500 (może być trwały błąd logiki)                   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    HttpResponseMessage
                              │
                              ▼
                  ┌───────────────────┐
                  │  Result Mapping    │
                  │  HTTP → ErrorType  │
                  └───────────────────┘
                              │
                              ▼
                         Result<T>
```

### Poziom 2: Result (po mapowaniu)

Obsługuje błędy na poziomie semantyki biznesowej:

```
┌─────────────────────────────────────────────────────────────┐
│               Result Level (Voyager.Common.Resilience)       │
├─────────────────────────────────────────────────────────────┤
│  Retry (BindWithRetryAsync):                                 │
│  • ErrorType.Unavailable → retry z exponential backoff       │
│  • ErrorType.Timeout → retry z exponential backoff           │
│  • Inne ErrorType → STOP, zwróć oryginalny błąd             │
│                                                              │
│  Circuit Breaker (BindWithCircuitBreakerAsync):             │
│  • Unavailable, Timeout, Database, Unexpected → liczy       │
│  • Validation, NotFound, Permission, etc. → ignoruje        │
│  • Po przekroczeniu progu → CircuitBreakerOpen              │
└─────────────────────────────────────────────────────────────┘
```

### Architektura integracji

```
┌─────────────────────────────────────────────────────────────────┐
│                     Service Proxy Client                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ IUserService.GetUserAsync(id)                            │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ ServiceProxy<T> (DispatchProxy)                          │   │
│  │ • Buduje HttpRequestMessage                              │   │
│  │ • Wywołuje HttpClient                                    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ HTTP Pipeline (HttpClientFactory + Handlers)             │   │
│  │                                                          │   │
│  │  ┌────────────────────────────────────────────────────┐  │   │
│  │  │ [Optional] HttpResilienceHandler                   │  │   │
│  │  │ • Retry: connection errors, 502/503/504            │  │   │
│  │  │ • Circuit breaker: consecutive transport failures  │  │   │
│  │  └────────────────────────────────────────────────────┘  │   │
│  │                          │                                │   │
│  │  ┌────────────────────────────────────────────────────┐  │   │
│  │  │ HttpClientHandler                                  │  │   │
│  │  │ • Actual HTTP call                                 │  │   │
│  │  └────────────────────────────────────────────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ HttpResponseMapper                                       │   │
│  │ • HTTP 200 → Result.Success(data)                        │   │
│  │ • HTTP 404 → Result.Failure(NotFound)                    │   │
│  │ • HTTP 503 → Result.Failure(Unavailable)                 │   │
│  │ • etc.                                                   │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│                         Result<T>                                │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

                              │
                              ▼
        ┌─────────────────────────────────────────────┐
        │ Application Code (opcjonalnie)              │
        │                                             │
        │ var result = await _userService             │
        │     .GetUserAsync(id)                       │
        │     .BindWithRetryAsync(                    │
        │         RetryPolicies.TransientErrors())    │
        │     .BindWithCircuitBreakerAsync(           │
        │         _circuitBreaker);                   │
        └─────────────────────────────────────────────┘
```

### API użycia

**Opcja A: Domyślna (bez resilience)**

```csharp
// Rejestracja
services.AddServiceProxy<IUserService>("https://api.example.com");

// Użycie - brak retry, brak circuit breaker
var result = await _userService.GetUserAsync(id);
```

**Opcja B: HTTP-level resilience (Polly)**

```csharp
// Rejestracja z Polly
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddStandardResilienceHandler();  // Microsoft.Extensions.Http.Resilience

// Lub custom
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))));
```

**Opcja C: Result-level resilience (Voyager.Common.Resilience)**

```csharp
// Rejestracja (bez Polly)
services.AddServiceProxy<IUserService>("https://api.example.com");
services.AddSingleton(new CircuitBreakerPolicy(failureThreshold: 5));

// Użycie z Result extensions
var result = await _userService.GetUserAsync(id)
    .BindWithRetryAsync(RetryPolicies.TransientErrors(maxAttempts: 3))
    .BindWithCircuitBreakerAsync(_circuitBreaker);
```

**Opcja D: Hybrydowa (rekomendowana dla krytycznych serwisów)**

```csharp
// Rejestracja - HTTP level dla transport errors
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()  // 5xx, 408, network errors
        .Or<TimeoutRejectedException>()
        .WaitAndRetryAsync(2, _ => TimeSpan.FromMilliseconds(500)));

// Circuit breaker na poziomie Result (świadomy semantyki błędów)
services.AddSingleton<CircuitBreakerPolicy>(sp =>
    new CircuitBreakerPolicy(
        failureThreshold: 5,
        openTimeout: TimeSpan.FromSeconds(30)));

// Użycie
var result = await _userService.GetUserAsync(id)
    .BindWithCircuitBreakerAsync(_circuitBreaker);
// HTTP retry już obsłużył transport errors
// Circuit breaker liczy tylko infrastructure errors (Unavailable, Timeout, Database)
```

### Mapowanie HTTP → ErrorType (aktualizacja)

| HTTP Status | ErrorType | Retry? | Circuit Breaker? |
|-------------|-----------|--------|------------------|
| 200-299 | Success | - | Reset |
| 400 | Validation | ❌ | ❌ Ignoruje |
| 401 | Unauthorized | ❌ | ❌ Ignoruje |
| 403 | Permission | ❌ | ❌ Ignoruje |
| 404 | NotFound | ❌ | ❌ Ignoruje |
| 408 | Timeout | ✅ | ✅ Liczy |
| 409 | Conflict | ❌ | ❌ Ignoruje |
| 429 | Unavailable | ✅ | ✅ Liczy |
| 500 | Unexpected | ❌ | ✅ Liczy |
| 502, 503, 504 | Unavailable | ✅ | ✅ Liczy |

### Konfiguracja domyślna

```csharp
public static class ResilienceDefaults
{
    // Retry
    public const int MaxRetryAttempts = 3;
    public const int BaseDelayMs = 1000;  // 1s, 2s, 4s (exponential)

    // Circuit Breaker
    public const int FailureThreshold = 5;
    public static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(30);
    public const int HalfOpenMaxAttempts = 3;
}
```

## Dlaczego ta opcja

### Korzyści dwupoziomowej architektury

1. **Separacja odpowiedzialności**
   - HTTP level: problemy transportowe (sieć, gateway)
   - Result level: problemy aplikacyjne (semantyka błędów)

2. **Świadomość kontekstu**
   - Polly nie wie, że 404 to "user not found" (nie retry!)
   - `Voyager.Common.Resilience` rozumie `ErrorType.NotFound`

3. **Unikanie podwójnego retry**
   - HTTP retry dla transport errors
   - Result retry dla application errors (jeśli potrzebne)
   - Jasny podział - brak nakładania się

4. **Spójność z ekosystemem Voyager**
   - `Voyager.Common.Results` definiuje semantykę błędów
   - `Voyager.Common.Resilience` implementuje polityki
   - `Voyager.Common.Proxy` integruje oba

### Porównanie z alternatywami

| Aspekt | Tylko Polly | Tylko Voyager.Resilience | Hybrydowa |
|--------|-------------|-------------------------|-----------|
| Transport errors | ✅ | ❌ (po mapowaniu) | ✅ |
| Semantyka ErrorType | ❌ | ✅ | ✅ |
| Circuit breaker aware | ❌ (HTTP codes) | ✅ (ErrorType) | ✅ |
| Złożoność | Niska | Niska | Średnia |
| Elastyczność | Średnia | Wysoka | Wysoka |

## Alternatywy które odrzuciliśmy

### Alternatywa 1: Tylko Polly na poziomie HTTP

```csharp
services.AddServiceProxy<IUserService>("https://api.example.com")
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());
```

**Dlaczego odrzucona:**
- Polly nie zna semantyki `Result<T>` i `ErrorType`
- Circuit breaker liczy wszystkie błędy 4xx/5xx jednakowo
- Retry na 404 jest bezcelowe (zasób nie istnieje)
- Brak integracji z `Voyager.Common.Resilience`

### Alternatywa 2: Tylko Result-level resilience

```csharp
var result = await _userService.GetUserAsync(id)
    .BindWithRetryAsync(RetryPolicies.TransientErrors())
    .BindWithCircuitBreakerAsync(_circuitBreaker);
```

**Dlaczego odrzucona:**
- Transport errors (connection refused) rzucają exception przed mapowaniem
- Wymaga try/catch w każdym miejscu użycia
- Niespójna obsługa błędów sieciowych vs aplikacyjnych

### Alternatywa 3: Własny HttpMessageHandler z integracją Result

```csharp
public class ResultAwareResilienceHandler : DelegatingHandler
{
    // Custom logic mapująca HTTP → Result → retry decision
}
```

**Dlaczego odrzucona:**
- Duplikacja logiki z `Voyager.Common.Resilience`
- Trudniejsze testowanie
- Handler nie ma dostępu do typu `T` w `Result<T>`

## Implementacja

### Faza 1: Dokumentacja i wytyczne

- [x] ADR definiujący strategię
- [x] Aktualizacja README z przykładami
- [x] Tabela mapowania HTTP → ErrorType w dokumentacji

### Faza 2: Integracja z Voyager.Common.Resilience

- [x] Dodanie referencji do `Voyager.Common.Resilience` (wymagana)
- [x] Extension methods dla łatwej integracji (`ResultResilienceExtensions`)
- [x] Przykłady w dokumentacji

### Faza 3: Konfiguracja przy rejestracji

- [x] `ResilienceOptions` z konfiguracją Retry i CircuitBreaker
- [x] Integracja z `ServiceProxyOptions`
- [x] Automatyczne stosowanie resilience dla wszystkich wywołań proxy

```csharp
// Zaimplementowane API
services.AddServiceProxy<IUserService>(options =>
{
    options.BaseUrl = new Uri("https://api.example.com");
    options.Resilience.Retry.Enabled = true;
    options.Resilience.Retry.MaxAttempts = 3;
    options.Resilience.CircuitBreaker.Enabled = true;
    options.Resilience.CircuitBreaker.FailureThreshold = 5;
});
```

## Ryzyka i mitigacje

| Ryzyko | Prawdopodobieństwo | Impact | Mitigacja |
|--------|-------------------|--------|-----------|
| Podwójny retry (HTTP + Result) | Średnie | Średni | Jasna dokumentacja; domyślnie tylko jeden poziom |
| Złożoność konfiguracji | Średnie | Niski | Sensowne defaults; helper methods |
| Performance overhead | Niskie | Niski | Lazy initialization; caching policies |
| Niespójność między projektami | Średnie | Średni | Shared configuration; code review guidelines |

## Metryki sukcesu

- Redukcja błędów transient o >80% w logach produkcyjnych
- Brak retry na błędach permanentnych (404, 400, 401)
- Circuit breaker aktywuje się tylko przy faktycznych awariach infrastruktury
- Czas recovery po awarii serwisu < 1 minuta

---

**Powiązane dokumenty:**
- [ADR-001: ServiceProxy Architecture](./ADR-001-ServiceProxy-Architecture.md)
- [Voyager.Common.Results](https://github.com/Voyager-Poland/Voyager.Common.Results)
- [Voyager.Common.Resilience](https://github.com/Voyager-Poland/Voyager.Common.Results/src/Voyager.Common.Resilience)
- [Microsoft.Extensions.Http.Resilience](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience)
