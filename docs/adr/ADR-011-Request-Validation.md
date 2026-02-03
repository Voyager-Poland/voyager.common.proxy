# ADR-011: Automatyczna walidacja requestów

**Status:** Zaimplementowane
**Data:** 2026-02-03
**Autor:** Claude Code

## Problem

W wielu modelach requestów istnieją metody walidacyjne:

```csharp
public abstract class PaymentModelRequestBase
{
    public CoachContext CoachContext { get; set; }
    public TicketPaymentItems TicketPaymentItem { get; set; }
    public bool IsInvoice { get; set; }

    public virtual Result IsValid()
    {
        return Result<PaymentModelRequestBase>.Success(this)
            .Ensure(m => m.CoachContext != null, Error.ValidationError("CoachContext is null"))
            .Ensure(m => m.TicketPaymentItem != null && m.TicketPaymentItem.Count > 0,
                Error.ValidationError("TicketPaymentItem is null or empty"));
    }
}
```

Obecnie te metody:
1. **Nie są automatycznie wywoływane** - programista musi pamiętać o wywołaniu
2. **Walidacja po stronie klienta** - błędy walidacji odkrywane dopiero po wykonaniu żądania HTTP
3. **Brak spójności** - niektóre modele zwracają `Result`, inne `bool`

## Propozycja

### Sposoby oznaczania walidacji

Wspieramy **dwa podejścia** - wybierz to, które lepiej pasuje do Twojego kodu:

#### Podejście 1: Interfejs (zalecane dla nowego kodu)

```csharp
namespace Voyager.Common.Proxy.Abstractions.Validation
{
    /// <summary>
    /// Interface for request models that support validation returning Result.
    /// </summary>
    public interface IValidatableRequest
    {
        /// <summary>
        /// Validates the request and returns a Result indicating success or validation errors.
        /// </summary>
        Result IsValid();
    }

    /// <summary>
    /// Interface for request models that support simple boolean validation.
    /// </summary>
    public interface IValidatableRequestBool
    {
        /// <summary>
        /// Validates the request and returns true if valid.
        /// </summary>
        bool IsValid();

        /// <summary>
        /// Gets the validation error message when IsValid returns false.
        /// </summary>
        string? ValidationErrorMessage { get; }
    }
}
```

#### Podejście 2: Atrybut na metodzie (dla istniejącego kodu)

```csharp
namespace Voyager.Common.Proxy.Abstractions.Validation
{
    /// <summary>
    /// Marks a method as the validation method for the request model.
    /// The method must return Result or bool.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ValidationMethodAttribute : Attribute
    {
        /// <summary>
        /// Optional error message when validation returns false (only for bool methods).
        /// If not specified, uses "Request validation failed".
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
```

#### Porównanie podejść

| Aspekt | Interfejs | Atrybut |
|--------|-----------|---------|
| Compile-time check | ✅ Tak | ❌ Nie |
| Istniejące modele | Wymaga zmiany | Tylko dodanie atrybutu |
| Wydajność | ✅ Szybkie (cast) | ⚠️ Wolniejsze (refleksja) |
| Nazwa metody | Musi być `IsValid()` | Dowolna |
| IntelliSense/refactoring | ✅ Pełne wsparcie | ⚠️ Ograniczone |

**Rekomendacja:** Używaj interfejsu dla nowego kodu, atrybutu dla integracji z istniejącymi modelami

### Atrybut do włączenia walidacji

```csharp
namespace Voyager.Common.Proxy.Abstractions
{
    /// <summary>
    /// Indicates that request parameters should be validated before processing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface)]
    public sealed class ValidateRequestAttribute : Attribute
    {
        /// <summary>
        /// If true, validation is performed ADDITIONALLY on client-side before HTTP call.
        /// Server-side validation ALWAYS happens regardless of this setting (for security).
        /// Default is false (server-side only).
        /// </summary>
        public bool ClientSide { get; set; } = false;
    }
}
```

### Zachowanie walidacji - podsumowanie

| Ustawienie | Klient | Serwer | Opis |
|------------|--------|--------|------|
| `[ValidateRequest]` | ❌ | ✅ | Tylko serwer (domyślnie) |
| `[ValidateRequest(ClientSide = true)]` | ✅ | ✅ | Klient + serwer (bezpieczeństwo) |

**Ważne:** Serwer ZAWSZE waliduje request, niezależnie od ustawienia `ClientSide`. Walidacja kliencka jest dodatkiem optymalizacyjnym, nie zamiennikiem walidacji serwerowej. Dzięki temu:
- Złośliwy klient nie może ominąć walidacji
- Nieaktualna wersja klienta nie narusza bezpieczeństwa
- Spójność danych jest gwarantowana po stronie serwera

### Miejsce walidacji

#### Tryb A: Tylko serwer `[ValidateRequest]` (domyślnie)

```
Client                                          Server
  │                                               │
  │  [ValidateRequest]                            │
  │                                               │
  ├─► HTTP Request ──────────────────────────────►│
  │                                               ├─► Deserialize request
  │                                               ├─► Call IsValid()
  │                                               │     │
  │◄── HTTP 400 + Error ──────────────────────────┤     ├─ Failure
  │                                               │     │
  │◄── HTTP 200 + Result ─────────────────────────┤◄────┴─ Success ──► Execute method
```

**Zalety:**
- Model requestu tylko po stronie serwera
- Centralna logika walidacji
- Spójne z obecną architekturą

**Wady:**
- Ruch sieciowy nawet dla nieprawidłowych requestów

#### Tryb B: Klient + Serwer `[ValidateRequest(ClientSide = true)]`

```
Client                                          Server
  │                                               │
  │  [ValidateRequest(ClientSide = true)]         │
  │                                               │
  ├─► Deserialize request                         │
  ├─► Call IsValid()                              │
  │     │                                         │
  │     ├─ Failure ──► Return Result.Failure()    │
  │     │     (bez ruchu sieciowego)              │
  │     │                                         │
  │     └─ Success ──► HTTP Request ─────────────►│
  │                                               ├─► Deserialize request
  │                                               ├─► Call IsValid()  ◄── ZAWSZE!
  │                                               │     │
  │◄── HTTP 400 + Error ──────────────────────────┤     ├─ Failure
  │                                               │     │
  │◄── HTTP 200 + Result ─────────────────────────┤◄────┴─ Success ──► Execute method
```

**Zalety:**
- Oszczędność ruchu sieciowego (większość błędów wykrywana na kliencie)
- Szybsza odpowiedź na błędy walidacji
- Mniejsze obciążenie serwera
- **Bezpieczeństwo zachowane** - serwer zawsze waliduje

**Wady:**
- Wymaga, by model requestu był dostępny po stronie klienta
- Logika walidacji musi być współdzielona
- Walidacja wykonywana dwukrotnie dla poprawnych requestów

### Implementacja - Strona serwera

#### Zmiany w RequestDispatcher

```csharp
public async Task DispatchAsync(...)
{
    // ... existing code ...

    try
    {
        // Bind parameters
        var parameters = await _parameterBinder.BindParametersAsync(context, endpoint);

        // NEW: Validate request parameters if method has [ValidateRequest]
        if (endpoint.Method.GetCustomAttribute<ValidateRequestAttribute>() != null ||
            endpoint.ServiceType.GetCustomAttribute<ValidateRequestAttribute>() != null)
        {
            ValidateParameters(parameters);  // Throws ArgumentException on failure
        }

        // Invoke method
        var result = endpoint.Method.Invoke(serviceInstance, parameters);
        // ... rest of existing code ...
    }
    catch (ArgumentException ex)
    {
        // EXISTING CODE - już obsługuje błędy walidacji
        // Emituje RequestCompletedEvent z ErrorType="Validation"
    }
}

private static void ValidateParameters(object?[] parameters)
{
    foreach (var param in parameters)
    {
        if (param == null) continue;

        // Podejście 1: Interfejs (szybkie - bez refleksji)
        if (param is IValidatableRequest validatable)
        {
            var result = validatable.IsValid();
            if (!result.IsSuccess)
            {
                throw new ArgumentException(result.Error.Message);
            }
            continue;
        }

        if (param is IValidatableRequestBool validatableBool)
        {
            if (!validatableBool.IsValid())
            {
                throw new ArgumentException(
                    validatableBool.ValidationErrorMessage ?? "Request validation failed");
            }
            continue;
        }

        // Podejście 2: Atrybut [ValidationMethod] (wolniejsze - refleksja)
        var validationMethod = param.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<ValidationMethodAttribute>() != null);

        if (validationMethod != null)
        {
            ValidateWithAttributeMethod(param, validationMethod);
        }
    }
}

private static void ValidateWithAttributeMethod(object param, MethodInfo method)
{
    var attr = method.GetCustomAttribute<ValidationMethodAttribute>()!;
    var returnType = method.ReturnType;
    var result = method.Invoke(param, null);

    // Obsługa Result
    if (typeof(Result).IsAssignableFrom(returnType) && result is Result resultValue)
    {
        if (!resultValue.IsSuccess)
        {
            throw new ArgumentException(resultValue.Error.Message);
        }
    }
    // Obsługa bool
    else if (returnType == typeof(bool) && result is bool boolValue)
    {
        if (!boolValue)
        {
            throw new ArgumentException(attr.ErrorMessage ?? "Request validation failed");
        }
    }
    else
    {
        throw new InvalidOperationException(
            $"[ValidationMethod] must return Result or bool, but {method.Name} returns {returnType.Name}");
    }
}
```

### Implementacja - Strona klienta

#### Zmiany w HttpMethodInterceptor

Walidacja kliencka jest wykonywana w metodzie `InterceptAsync` przed wykonaniem żądania HTTP:

```csharp
public async Task<object?> InterceptAsync(MethodInfo method, object?[] arguments)
{
    // ... validation of return type ...

    // Client-side validation (if enabled)
    if (ShouldValidateClientSide(method))
    {
        var validationError = ValidateArguments(arguments);
        if (validationError != null)
        {
            return CreateFailureResult(resultType, validationError);
        }
    }

    return await ExecuteWithResilienceAsync(method, arguments, resultType).ConfigureAwait(false);
}

private static bool ShouldValidateClientSide(MethodInfo method)
{
    // Check method-level attribute first
    var methodAttr = method.GetCustomAttribute<ValidateRequestAttribute>();
    if (methodAttr != null)
    {
        return methodAttr.ClientSide;
    }

    // Check interface-level attribute
    var interfaceAttr = method.DeclaringType?.GetCustomAttribute<ValidateRequestAttribute>();
    if (interfaceAttr != null)
    {
        return interfaceAttr.ClientSide;
    }

    return false;
}

private static Error? ValidateArguments(object?[] arguments)
{
    foreach (var arg in arguments)
    {
        if (arg == null) continue;

        // Podejście 1: Interfejs - IValidatableRequest
        if (arg is IValidatableRequest validatable)
        {
            var result = validatable.IsValid();
            if (!result.IsSuccess)
            {
                return result.Error;
            }
            continue;
        }

        // Podejście 1: Interfejs - IValidatableRequestBool
        if (arg is IValidatableRequestBool validatableBool)
        {
            if (!validatableBool.IsValid())
            {
                var message = validatableBool.ValidationErrorMessage ?? "Request validation failed";
                return Error.ValidationError(message);
            }
            continue;
        }

        // Podejście 2: Atrybut [ValidationMethod]
        var validationMethod = arg.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<ValidationMethodAttribute>() != null);

        if (validationMethod != null)
        {
            var error = ValidateWithAttributeMethod(arg, validationMethod);
            if (error != null)
            {
                return error;
            }
        }
    }

    return null;
}
```

### Przykłady użycia

#### Podejście 1: Interfejs

##### Model z Result walidacją

```csharp
public class CreatePaymentRequest : IValidatableRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public PaymentMethod Method { get; set; }

    public Result IsValid()
    {
        return Result.Success()
            .Ensure(() => Amount > 0, Error.ValidationError("Amount must be positive"))
            .Ensure(() => !string.IsNullOrEmpty(Currency), Error.ValidationError("Currency is required"))
            .Ensure(() => Currency.Length == 3, Error.ValidationError("Currency must be 3 characters"));
    }
}
```

##### Model z bool walidacją

```csharp
public class SimpleRequest : IValidatableRequestBool
{
    public int Id { get; set; }

    public bool IsValid() => Id > 0;

    public string? ValidationErrorMessage => Id <= 0 ? "Id must be positive" : null;
}
```

#### Podejście 2: Atrybut (dla istniejącego kodu)

##### Istniejący model - tylko dodajemy atrybut

```csharp
// Istniejący model - nie zmieniamy hierarchii dziedziczenia
public class LegacyPaymentRequest
{
    public decimal Amount { get; set; }
    public string Currency { get; set; }

    // Istniejąca metoda walidacyjna - tylko dodajemy atrybut
    [ValidationMethod]
    public Result Validate()  // ← Nazwa dowolna, nie musi być IsValid()
    {
        return Result.Success()
            .Ensure(() => Amount > 0, Error.ValidationError("Amount must be positive"))
            .Ensure(() => !string.IsNullOrEmpty(Currency), Error.ValidationError("Currency is required"));
    }
}
```

##### Model z bool i custom komunikatem

```csharp
public class SimpleBookingRequest
{
    public int BookingId { get; set; }
    public DateTime Date { get; set; }

    [ValidationMethod(ErrorMessage = "BookingId must be positive and date cannot be in the past")]
    public bool CheckValid()  // ← Dowolna nazwa
    {
        return BookingId > 0 && Date >= DateTime.Today;
    }
}
```

#### Interfejs serwisu

```csharp
[ValidateRequest]  // Walidacja dla wszystkich metod
public interface IPaymentService
{
    Task<Result<PaymentResponse>> CreatePaymentAsync(CreatePaymentRequest request);

    [ValidateRequest(ClientSide = true)]  // Walidacja po stronie klienta
    Task<Result> ValidatePaymentAsync(ValidatePaymentRequest request);
}
```

### Diagnostyka

**Bez nowego eventu** - istniejący system diagnostyczny już obsługuje błędy walidacji.

Obecny `RequestDispatcher` przy `ArgumentException` emituje:

```csharp
emitter.EmitRequestCompleted(new RequestCompletedEvent
{
    StatusCode = 400,
    IsSuccess = false,
    ErrorType = "Validation",
    ErrorMessage = ex.Message,
    // + TraceId, SpanId, ParentSpanId, UserLogin, UnitId, UnitType
});
```

### Przepływ walidacji - kto co robi

```
Model (IsValid)              Infrastruktura (RequestDispatcher)
     │                                    │
     │  zwraca Result                     │
     │  (NIE rzuca wyjątku)               │
     ▼                                    ▼
┌──────────────────┐              ┌──────────────────────────────┐
│ IsValid()        │              │ ValidateParameters()         │
│ {                │              │ {                            │
│   return Result  │   ────►      │   var result = IsValid();    │
│     .Ensure(...) │              │   if (!result.IsSuccess)     │
│     .Ensure(...);│              │     throw ArgumentException; │◄── Tu rzucamy
│ }                │              │ }                            │
└──────────────────┘              └──────────────────────────────┘
```

**Ważne:** Model pozostaje spójny z resztą kodu - zwraca `Result`, nie rzuca wyjątku. Konwersja na `ArgumentException` następuje w kodzie infrastrukturalnym (`RequestDispatcher`), który już obsługuje ten typ wyjątku i emituje odpowiednie eventy diagnostyczne.

## Decyzja

1. **Miejsce walidacji:** Serwer ZAWSZE, klient jako dodatkowa opcja (`ClientSide = true`)
2. **Bezpieczeństwo:** Walidacja kliencka nie zastępuje serwerowej - obie działają równolegle
3. **Tryb:** Opt-in przez atrybut `[ValidateRequest]` na metodzie/interfejsie
4. **Dwa podejścia do oznaczania walidacji:**
   - **Interfejs** (`IValidatableRequest`, `IValidatableRequestBool`) - zalecane dla nowego kodu
   - **Atrybut** (`[ValidationMethod]`) - dla integracji z istniejącymi modelami
5. **Typy zwracane:** Oba (`Result` i `bool`) dla kompatybilności
6. **Diagnostyka:** Użyć istniejącego `RequestCompletedEvent` z `ErrorType="Validation"`

## Implementacja

### Faza 1: Abstrakcje ✅

- [x] Dodać `IValidatableRequest` do Abstractions → [IValidatableRequest.cs](../../src/Voyager.Common.Proxy.Abstractions/Validation/IValidatableRequest.cs)
- [x] Dodać `IValidatableRequestBool` do Abstractions → [IValidatableRequestBool.cs](../../src/Voyager.Common.Proxy.Abstractions/Validation/IValidatableRequestBool.cs)
- [x] Dodać `ValidateRequestAttribute` do Abstractions → [ValidateRequestAttribute.cs](../../src/Voyager.Common.Proxy.Abstractions/Validation/ValidateRequestAttribute.cs)
- [x] Dodać `ValidationMethodAttribute` do Abstractions → [ValidationMethodAttribute.cs](../../src/Voyager.Common.Proxy.Abstractions/Validation/ValidationMethodAttribute.cs)

### Faza 2: Walidacja serwerowa ✅

- [x] Zmodyfikować `RequestDispatcher` - walidacja przed wywołaniem metody → [RequestDispatcher.cs](../../src/Voyager.Common.Proxy.Server.Core/RequestDispatcher.cs)
- [x] Dodać `RequestValidator` - logika walidacji → [RequestValidator.cs](../../src/Voyager.Common.Proxy.Server.Core/RequestValidator.cs)
- [x] Obsługa `IValidatableRequest` (zwraca `Result`)
- [x] Obsługa `IValidatableRequestBool` (zwraca `bool`)
- [x] Obsługa `[ValidationMethod]` (refleksja - dla istniejących modeli)
- [x] Rzucanie `ArgumentException` przy błędach (integracja z istniejącą diagnostyką)
- [x] Testy jednostkowe → [RequestValidationTests.cs](../../tests/Voyager.Common.Proxy.Server.Tests/RequestValidationTests.cs) (11 testów)

### Faza 3: Walidacja kliencka ✅

- [x] Zmodyfikować `HttpMethodInterceptor` - walidacja przed HTTP call → [HttpMethodInterceptor.cs](../../src/Voyager.Common.Proxy.Client/Internal/HttpMethodInterceptor.cs)
- [x] Testy jednostkowe → [ClientValidationTests.cs](../../tests/Voyager.Common.Proxy.Client.Tests/ClientValidationTests.cs) (15 testów)

## Testy

### Testy walidacji serwerowej (RequestValidationTests.cs)

| Test | Opis |
|------|------|
| `Dispatch_WithValidIValidatableRequest_Succeeds` | Poprawny request z IValidatableRequest przechodzi |
| `Dispatch_WithInvalidIValidatableRequest_ReturnsValidationError` | Niepoprawny request zwraca błąd walidacji |
| `Dispatch_WithMissingCurrency_ReturnsValidationError` | Brak wymaganego pola zwraca błąd |
| `Dispatch_WithValidIValidatableRequestBool_Succeeds` | Poprawny request z IValidatableRequestBool przechodzi |
| `Dispatch_WithInvalidIValidatableRequestBool_ReturnsValidationError` | Niepoprawny bool request zwraca błąd |
| `Dispatch_WithValidAttributeValidatedRequest_Succeeds` | Request z [ValidationMethod] przechodzi |
| `Dispatch_WithInvalidAttributeValidatedRequest_ReturnsValidationError` | Niepoprawny [ValidationMethod] zwraca błąd |
| `Dispatch_WithValidAttributeBoolValidatedRequest_Succeeds` | Bool [ValidationMethod] przechodzi |
| `Dispatch_WithInvalidAttributeBoolValidatedRequest_ReturnsValidationError` | Niepoprawny bool [ValidationMethod] zwraca błąd |
| `Dispatch_WithoutValidateRequestAttribute_SkipsValidation` | Bez atrybutu walidacja jest pomijana |
| `Dispatch_WithRegularRequest_Succeeds` | Zwykły request bez walidacji działa |

### Testy walidacji klienckiej (ClientValidationTests.cs)

| Test | Opis |
|------|------|
| `ClientValidation_WithValidIValidatableRequest_MakesRequest` | Poprawny request wykonuje HTTP |
| `ClientValidation_WithInvalidIValidatableRequest_ReturnsErrorWithoutRequest` | Niepoprawny request nie wykonuje HTTP |
| `ClientValidation_WithMissingCurrency_ReturnsErrorWithoutRequest` | Brak pola nie wykonuje HTTP |
| `ClientValidation_WithValidIValidatableRequestBool_MakesRequest` | Bool request wykonuje HTTP |
| `ClientValidation_WithInvalidIValidatableRequestBool_ReturnsErrorWithoutRequest` | Niepoprawny bool nie wykonuje HTTP |
| `ClientValidation_WithValidAttributeValidatedRequest_MakesRequest` | [ValidationMethod] wykonuje HTTP |
| `ClientValidation_WithInvalidAttributeValidatedRequest_ReturnsErrorWithoutRequest` | Niepoprawny [ValidationMethod] nie wykonuje HTTP |
| `ClientValidation_WithValidAttributeBoolValidatedRequest_MakesRequest` | Bool [ValidationMethod] wykonuje HTTP |
| `ClientValidation_WithInvalidAttributeBoolValidatedRequest_ReturnsErrorWithoutRequest` | Niepoprawny bool [ValidationMethod] nie wykonuje HTTP |
| `NoClientValidation_WithServerOnlyAttribute_MakesRequestEvenIfInvalid` | ClientSide=false wykonuje HTTP |
| `NoClientValidation_WithoutAttribute_MakesRequestEvenIfInvalid` | Bez atrybutu wykonuje HTTP |
| `NoClientValidation_WithRegularRequest_MakesRequest` | Zwykły request wykonuje HTTP |
| `MethodLevelValidation_WithClientSideTrue_ValidatesOnClient` | Atrybut na metodzie działa |
| `MethodLevelValidation_WithClientSideFalse_DoesNotValidateOnClient` | ClientSide=false na metodzie pomija walidację |

## Powiązane dokumenty

- [ADR-008: Diagnostics Strategy](./ADR-008-Diagnostics-Strategy.md)
- [Voyager.Common.Results](https://github.com/Voyager-Poland/Voyager.Common.Results)
