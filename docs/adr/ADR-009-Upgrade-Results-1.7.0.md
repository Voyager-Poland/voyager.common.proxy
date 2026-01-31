# ADR-009: Upgrade do Voyager.Common.Results 1.7.0

**Status:** Zaimplementowano
**Data:** 2026-01-31
**Kontekst:** Voyager.Common.Results 1.7.0.preview.1

## Problem

Projekt zawiera zduplikowaną logikę klasyfikacji błędów w wielu miejscach:

| Lokalizacja | Duplikacja |
|-------------|------------|
| `HttpMethodInterceptor.IsTransientError()` | Prywatna metoda - hardcoded enum comparison |
| `ResultResilienceExtensions.IsTransient()` | Publiczna metoda - ta sama logika |
| `ResultResilienceExtensions.IsInfrastructureFailure()` | Hardcoded lista ErrorType |
| `AspNetCoreResponseWriter.MapErrorTypeToStatusCode()` | 25 case'ów switch |
| `OwinResponseWriter.MapErrorTypeToStatusCode()` | Identyczny switch zduplikowany |
| `ResultMapper.MapStatusCodeToError()` | Odwrotne mapowanie HTTP → Error |

**Voyager.Common.Results 1.7.0** dostarcza te metody centralnie:
- `error.Type.IsTransient()`
- `error.Type.IsBusinessError()`
- `error.Type.IsInfrastructureError()`
- `error.Type.ShouldCountForCircuitBreaker()`
- `error.Type.ToHttpStatusCode()`
- `Error.FromException()` z pełnymi szczegółami
- `error.InnerError` - łańcuch błędów

## Decyzja

Upgrade do `Voyager.Common.Results 1.7.0` i usunięcie zduplikowanej logiki.

## Zmiany

### 1. Package References

```xml
<!-- Przed -->
<PackageReference Include="Voyager.Common.Results" Version="1.6.0" />

<!-- Po -->
<PackageReference Include="Voyager.Common.Results" Version="1.7.0" />
```

**Pliki:**
- `src/Voyager.Common.Proxy.Client/Voyager.Common.Proxy.Client.csproj`
- `src/Voyager.Common.Proxy.Server.Core/Voyager.Common.Proxy.Server.Core.csproj`
- Wszystkie projekty testowe

### 2. HttpMethodInterceptor.cs - Usunięcie duplikacji

**Przed:**
```csharp
private static bool IsTransientError(Error? error)
{
    if (error == null) return false;
    return error.Type == ErrorType.Unavailable
        || error.Type == ErrorType.Timeout;
}

// Użycie:
if (!IsTransientError(error) || attempt >= maxAttempts)
```

**Po:**
```csharp
using Voyager.Common.Results.Extensions;

// Usunąć metodę IsTransientError()

// Użycie:
if (error is null || !error.Type.IsTransient() || attempt >= maxAttempts)
```

**Lokalizacja:** `src/Voyager.Common.Proxy.Client/Internal/HttpMethodInterceptor.cs`

### 3. ResultResilienceExtensions.cs - Delegacja do biblioteki

**Przed:**
```csharp
public static bool IsTransient(this Error error)
{
    return error.Type == ErrorType.Unavailable
        || error.Type == ErrorType.Timeout;
}

public static bool IsInfrastructureFailure(this Error error)
{
    return error.Type == ErrorType.Unavailable
        || error.Type == ErrorType.Timeout
        || error.Type == ErrorType.Database
        || error.Type == ErrorType.Unexpected;
}
```

**Po (Opcja A - usunięcie):**
```csharp
// Usunąć metody - użytkownicy używają bezpośrednio:
// error.Type.IsTransient()
// error.Type.IsInfrastructureError()
```

**Po (Opcja B - zachowanie dla kompatybilności):**
```csharp
using Voyager.Common.Results.Extensions;

[Obsolete("Use error.Type.IsTransient() from Voyager.Common.Results.Extensions")]
public static bool IsTransient(this Error error)
    => error.Type.IsTransient();

[Obsolete("Use error.Type.IsInfrastructureError() from Voyager.Common.Results.Extensions")]
public static bool IsInfrastructureFailure(this Error error)
    => error.Type.IsInfrastructureError();
```

**Lokalizacja:** `src/Voyager.Common.Proxy.Client/Extensions/ResultResilienceExtensions.cs`

### 4. AspNetCoreResponseWriter.cs - Użycie ToHttpStatusCode()

**Przed:**
```csharp
private static int MapErrorTypeToStatusCode(string errorType)
{
    return errorType switch
    {
        "Validation" => StatusCodes.Status400BadRequest,
        "Business" => StatusCodes.Status400BadRequest,
        "NotFound" => StatusCodes.Status404NotFound,
        "Unauthorized" => StatusCodes.Status401Unauthorized,
        "Permission" => StatusCodes.Status403Forbidden,
        // ... 20+ więcej case'ów
        _ => StatusCodes.Status500InternalServerError
    };
}
```

**Po:**
```csharp
using Voyager.Common.Results.Extensions;

// Usunąć metodę MapErrorTypeToStatusCode()

// W WriteErrorAsync():
var statusCode = Enum.TryParse<ErrorType>(errorType, out var type)
    ? type.ToHttpStatusCode()
    : 500;
```

**Lokalizacja:** `src/Voyager.Common.Proxy.Server.AspNetCore/AspNetCoreResponseWriter.cs`

### 5. OwinResponseWriter.cs - Analogiczna zmiana

**Przed:**
```csharp
private static int MapErrorTypeToStatusCode(string errorType)
{
    return errorType switch
    {
        "Validation" => 400,
        "Business" => 400,
        // ... identyczna duplikacja
        _ => 500
    };
}
```

**Po:**
```csharp
using Voyager.Common.Results.Extensions;

// Usunąć metodę MapErrorTypeToStatusCode()

// W WriteErrorAsync():
var statusCode = Enum.TryParse<ErrorType>(errorType, out var type)
    ? type.ToHttpStatusCode()
    : 500;
```

**Lokalizacja:** `src/Voyager.Common.Proxy.Server.Owin/OwinResponseWriter.cs`

### 6. ResultMapper.cs - Mapowanie HTTP → Error

**Przed:**
```csharp
private static Error MapStatusCodeToError(HttpStatusCode statusCode, string message)
{
    return statusCode switch
    {
        HttpStatusCode.BadRequest => Error.ValidationError(message),
        HttpStatusCode.RequestTimeout => Error.TimeoutError(message),
        (HttpStatusCode)429 => Error.UnavailableError(message),
        HttpStatusCode.ServiceUnavailable => Error.UnavailableError(message),
        HttpStatusCode.GatewayTimeout => Error.TimeoutError(message),
        HttpStatusCode.InternalServerError => Error.UnexpectedError(message),
        _ => Error.UnexpectedError($"HTTP {(int)statusCode}: {message}")
    };
}
```

**Po:**
```csharp
private static Error MapStatusCodeToError(HttpStatusCode statusCode, string message)
{
    return (int)statusCode switch
    {
        400 => Error.ValidationError(message),
        401 => Error.UnauthorizedError(message),
        403 => Error.PermissionError(message),
        404 => Error.NotFoundError(message),
        408 => Error.TimeoutError(message),
        409 => Error.ConflictError(message),
        429 => Error.TooManyRequestsError(message),  // NOWY TYP!
        499 => Error.CancelledError(message),
        503 => Error.UnavailableError(message),
        504 => Error.TimeoutError(message),
        _ => Error.UnexpectedError($"HTTP {(int)statusCode}: {message}")
    };
}
```

**Uwaga:** Teraz możemy użyć `Error.TooManyRequestsError()` zamiast `UnavailableError()` dla 429.

**Lokalizacja:** `src/Voyager.Common.Proxy.Client/Internal/ResultMapper.cs`

### 7. Nowa funkcjonalność: Error Chaining

W `HttpMethodInterceptor` przy retry możemy teraz zachować łańcuch błędów:

```csharp
// Przy ostatniej próbie retry - zachowaj kontekst
if (attempt == maxAttempts && lastError is not null)
{
    var wrappedError = Error.UnavailableError(
        "Proxy.RetryExhausted",
        $"All {maxAttempts} retry attempts failed")
        .WithInner(lastError);

    return Result<TResult>.Failure(wrappedError);
}
```

### 8. Nowa funkcjonalność: Exception Details

W `Result.TryAsync` zachowujemy teraz stack trace:

```csharp
// Przed (1.6.0)
catch (Exception ex)
{
    return Result<T>.Failure(Error.FromException(ex));
    // Tracimy: StackTrace, ExceptionType, Source
}

// Po (1.7.0) - automatycznie zachowuje szczegóły
catch (Exception ex)
{
    return Result<T>.Failure(Error.FromException(ex));
    // Zachowuje: StackTrace, ExceptionType, Source, InnerError chain
}
```

## Podsumowanie zmian

| Plik | Akcja | Linie kodu |
|------|-------|------------|
| `HttpMethodInterceptor.cs` | Usunąć `IsTransientError()`, użyć extension | -10 |
| `ResultResilienceExtensions.cs` | Usunąć/oznaczyć obsolete | -15 |
| `AspNetCoreResponseWriter.cs` | Usunąć `MapErrorTypeToStatusCode()` | -30 |
| `OwinResponseWriter.cs` | Usunąć `MapErrorTypeToStatusCode()` | -30 |
| `ResultMapper.cs` | Dodać `TooManyRequestsError` | +2 |
| `*.csproj` | Upgrade wersji | ~0 |

**Bilans: ~-80 linii kodu** przy zachowaniu tej samej funkcjonalności.

## Kompatybilność wsteczna

| Aspekt | Status | Uwagi |
|--------|--------|-------|
| API publiczne | ✅ Bez zmian | Metody resilience zachowane |
| Zachowanie retry | ✅ Bez zmian | Ta sama klasyfikacja transient |
| Zachowanie CB | ⚠️ Drobna zmiana | `CircuitBreakerOpen` nie liczy się do CB |
| HTTP mapping | ✅ Bez zmian | Te same kody statusu |
| Error 429 | ⚠️ Zmiana typu | `Unavailable` → `TooManyRequests` |

## Testy do aktualizacji

1. `ResultResilienceExtensionsTests.cs` - jeśli usuwamy metody
2. Testy integracyjne retry - sprawdzić czy `TooManyRequests` jest retryable
3. Testy response writerów - sprawdzić mapowanie

## Plan implementacji

- [ ] 1. Upgrade package references do 1.7.0.preview.1
- [ ] 2. Dodać `using Voyager.Common.Results.Extensions` gdzie potrzeba
- [ ] 3. Usunąć `HttpMethodInterceptor.IsTransientError()`
- [ ] 4. Usunąć/oznaczyć obsolete metody w `ResultResilienceExtensions`
- [ ] 5. Usunąć `MapErrorTypeToStatusCode()` z obu ResponseWriterów
- [ ] 6. Zaktualizować `ResultMapper.MapStatusCodeToError()` o 429
- [ ] 7. Uruchomić testy, naprawić co trzeba
- [ ] 8. Opcjonalnie: dodać error chaining w retry logic

---

**Powiązane:**
- [ADR-007: Resilience Strategy](./ADR-007-Resilience-Strategy.md)
- [Voyager.Common.Results ADR-0005: Error Classification](file:///C:/src/Voyager.Common.Results/docs/adr/ADR-0005-error-classification-for-resilience.md)
- [Voyager.Common.Results ADR-0006: Error Chaining](file:///C:/src/Voyager.Common.Results/docs/adr/ADR-0006-error-chaining-for-distributed-systems.md)
- [Voyager.Common.Results ADR-0007: Exception Details](file:///C:/src/Voyager.Common.Results/docs/adr/ADR-0007-exception-details-preservation.md)
