# ADR-012: Obsługa pustego prefixu w ServiceRouteAttribute

**Status:** Zaimplementowane (Opcja B)
**Data:** 2026-02-06
**Autor:** Claude Code

## Problem

Istnieją serwisy zewnętrzne (np. ePay), które nie posiadają wspólnego prefixu routingu. Ich endpointy są dostępne bezpośrednio pod root URL:

```
POST /NewOrder
POST /CreateOrder
POST /GetOrder
POST /ConfirmPayment
...
```

Interfejs kliencki dla takiego serwisu naturalnie wygląda tak:

```csharp
[ServiceRoute("")]
[ValidateRequest(ClientSide = true)]
public interface IEPayApiCommunicationResult
{
    [HttpPost("NewOrder")]
    Task<Result<Order>> NewOrder(Order order, CancellationToken cancellationToken = default);

    [HttpPost("GetOrder")]
    Task<Result<Order>> GetOrder(uint orderId, CancellationToken cancellationToken = default);
    // ...
}
```

Jednak `ServiceRouteAttribute` rzuca wyjątek dla pustego stringa:

```
System.ArgumentException : Route prefix cannot be empty or whitespace. (Parameter 'prefix')
```

**Stack trace:**
```
ServiceRouteAttribute.ctor(String prefix)
  → RouteBuilder.GetServicePrefix(Type interfaceType)
    → HttpMethodInterceptor.ctor(...)
      → ServiceProxy<T>.Create(...)
```

### Kontekst techniczny

Metoda `CombinePaths` w `RouteBuilder` **już poprawnie obsługuje** pusty prefix:

```csharp
private static string CombinePaths(string prefix, string path)
{
    prefix = prefix.Trim('/');
    path = path.TrimStart('/');

    if (string.IsNullOrEmpty(prefix))
    {
        return "/" + path;   // ← Poprawnie: "/NewOrder"
    }

    return "/" + prefix + "/" + path;
}
```

Problem leży wyłącznie w walidacji konstruktora `ServiceRouteAttribute` (linie 73-76), która blokuje pusty string zanim ten dotrze do `CombinePaths`.

### Dlaczego konwencja (brak atrybutu) nie rozwiązuje problemu

Bez `[ServiceRoute]` prefix jest generowany z nazwy interfejsu:
- `IEPayApiCommunicationResult` → `e-pay-api-communication-result`
- URL wynikowy: `/e-pay-api-communication-result/NewOrder` ← **błędny**

Serwis ePay oczekuje: `/NewOrder` (bez prefixu).

## Propozycje rozwiązań

### Opcja A: Dopuścić pusty string w ServiceRouteAttribute (rekomendowana)

Zmiana walidacji - usunąć blokadę pustego stringa, zachować blokadę whitespace i null:

```csharp
public ServiceRouteAttribute(string prefix)
{
    if (prefix is null)
    {
        throw new ArgumentNullException(nameof(prefix));
    }

    // Pusty string jest dozwolony - oznacza brak prefixu
    // Whitespace-only jest nadal zabroniony (prawdopodobny błąd)
    if (prefix.Length > 0 && string.IsNullOrWhiteSpace(prefix))
    {
        throw new ArgumentException(
            "Route prefix cannot contain only whitespace. Use empty string for no prefix.",
            nameof(prefix));
    }

    Prefix = prefix.Trim('/');
}
```

**Wpływ na routing:**
| Konfiguracja | Prefix | URL dla `[HttpPost("NewOrder")]` |
|---|---|---|
| `[ServiceRoute("api/v2")]` | `"api/v2"` | `/api/v2/NewOrder` |
| `[ServiceRoute("")]` | `""` | `/NewOrder` |
| Brak atrybutu | `"e-pay-api-..."` | `/e-pay-api-.../NewOrder` |

**Zalety:**
- Minimalna zmiana (3-4 linie kodu)
- `CombinePaths` już obsługuje ten przypadek - zero zmian w logice routingu
- Semantyka jasna: `[ServiceRoute("")]` = "brak prefixu"
- Wstecznie kompatybilne - istniejący kod nie jest affected

**Wady:**
- Pusty string może być nieintuicyjny dla nowych developerów
- Brak wyraźnego rozróżnienia między "celowo pusty" a "zapomniałem ustawić"

### Opcja B: Dodać stałą `ServiceRouteAttribute.NoPrefix`

```csharp
public sealed class ServiceRouteAttribute : Attribute
{
    /// <summary>
    /// Use this constant to explicitly indicate that the service has no route prefix.
    /// </summary>
    /// <example>
    /// <code>
    /// [ServiceRoute(ServiceRouteAttribute.NoPrefix)]
    /// public interface IExternalApiService { ... }
    /// </code>
    /// </example>
    public const string NoPrefix = "";

    public string Prefix { get; }

    public ServiceRouteAttribute(string prefix)
    {
        if (prefix is null)
        {
            throw new ArgumentNullException(nameof(prefix));
        }

        if (prefix.Length > 0 && string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException(
                "Route prefix cannot contain only whitespace. Use ServiceRouteAttribute.NoPrefix for no prefix.",
                nameof(prefix));
        }

        Prefix = prefix.Trim('/');
    }
}
```

Użycie:
```csharp
[ServiceRoute(ServiceRouteAttribute.NoPrefix)]
public interface IEPayApiCommunicationResult { ... }
```

**Zalety:**
- Wszystko z Opcji A
- Bardziej czytelne i samodokumentujące się (`NoPrefix` vs `""`)
- IntelliSense podpowiada stałą
- Jasna intencja: developer świadomie wybrał brak prefixu

**Wady:**
- Trochę więcej kodu do napisania przy użyciu (`ServiceRouteAttribute.NoPrefix` vs `""`)
- Nadal pozwala na `[ServiceRoute("")]` bezpośrednio

### Opcja C: Osobny atrybut `[NoServiceRoute]`

```csharp
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
public sealed class NoServiceRouteAttribute : Attribute
{
}
```

W `RouteBuilder.GetServicePrefix`:
```csharp
public static string GetServicePrefix(Type interfaceType)
{
    // Sprawdź czy jawnie wyłączono prefix
    if (interfaceType.GetCustomAttribute<NoServiceRouteAttribute>() != null)
    {
        return string.Empty;
    }

    var attribute = interfaceType.GetCustomAttribute<ServiceRouteAttribute>();
    if (attribute != null)
    {
        return attribute.Prefix;
    }

    // Convention: IUserService -> user-service
    // ...
}
```

Użycie:
```csharp
[NoServiceRoute]
public interface IEPayApiCommunicationResult { ... }
```

**Zalety:**
- Bardzo czytelne - nazwa atrybutu wyraża intencję
- Nie zmienia istniejącego `ServiceRouteAttribute`
- Prosta implementacja

**Wady:**
- Nowy typ atrybutu do utrzymania
- Potencjalne konflikty: co gdy ktoś da oba `[ServiceRoute("x")]` i `[NoServiceRoute]`?
- Zmiana w `RouteBuilder.GetServicePrefix` po stronie klienta i potencjalnie serwera
- Nie rozwiązuje problemu dla `[ServiceRoute("")]` - nadal będzie rzucać wyjątek, co może mylić

### Opcja D: Obsłużyć brak atrybutu z możliwością override konwencji

Dodać flagę do `ServiceProxyOptions` pozwalającą na nadpisanie prefixu:

```csharp
builder.Services.AddServiceProxy<IEPayApiCommunicationResult>(options =>
{
    options.BaseUrl = "https://epay.example.com";
    options.RoutePrefix = "";  // Override konwencji
});
```

**Zalety:**
- Nie zmienia atrybutów
- Konfiguracja w jednym miejscu (DI setup)

**Wady:**
- Prefix jest konfiguracyjny, nie deklaratywny - łatwiej o rozsynchronizowanie
- Wymaga zmian w `ServiceProxyOptions`, `HttpMethodInterceptor`, i potencjalnie `ServiceProxy<T>`
- Interfejs nie wyraża swojego routingu - trzeba szukać w konfiguracji DI
- Serwer nie wie o tym ustawieniu (dotyczy tylko klienta)

## Porównanie opcji

| Aspekt | A: Pusty string | B: Stała NoPrefix | C: Osobny atrybut | D: Opcja w config |
|--------|:---:|:---:|:---:|:---:|
| Zmian w kodzie | ~5 linii | ~10 linii | ~20 linii | ~30 linii |
| Czytelność | Średnia | Wysoka | Wysoka | Niska |
| Wsteczna kompatybilność | Pełna | Pełna | Pełna | Pełna |
| Deklaratywność | Tak | Tak | Tak | Nie |
| Samodokumentowanie | Słabe | Dobre | Dobre | Słabe |
| Ryzyko pomyłki | Niskie | Bardzo niskie | Bardzo niskie | Średnie |
| Spójność z istniejącym API | Wysoka | Wysoka | Średnia | Niska |

## Rekomendacja

**Opcja B: Stała `ServiceRouteAttribute.NoPrefix`** - łączy prostotę Opcji A z czytelnością:

1. Minimalna zmiana w `ServiceRouteAttribute` (dopuszczenie pustego stringa + stała)
2. `CombinePaths` działa bez zmian
3. Jasna intencja przez `ServiceRouteAttribute.NoPrefix`
4. Pusty string `""` też zadziała (dla tych, co znają API)
5. Prosta migracja istniejących interfejsów: `[ServiceRoute("")]` → `[ServiceRoute(ServiceRouteAttribute.NoPrefix)]`

## Implementacja (Opcja B)

### Faza 1: Zmiana atrybutu ✅

- [x] Zmodyfikować `ServiceRouteAttribute` - dodać stałą `NoPrefix`, zmienić walidację → [ServiceRouteAttribute.cs](../../src/Voyager.Common.Proxy.Abstractions/Attributes/ServiceRouteAttribute.cs)
- [x] Testy jednostkowe dla pustego prefixu → [ServiceRouteAttributeTests.cs](../../tests/Voyager.Common.Proxy.Client.Tests/ServiceRouteAttributeTests.cs) (7 testów)

### Faza 2: Testy routingu ✅

- [x] Test `RouteBuilder.GetServicePrefix` z pustym prefixem (NoPrefix i "") → [RouteBuilderTests.cs](../../tests/Voyager.Common.Proxy.Client.Tests/RouteBuilderTests.cs)
- [x] Test `RouteBuilder.BuildRequest` z pustym prefixem (template i konwencja) → [RouteBuilderTests.cs](../../tests/Voyager.Common.Proxy.Client.Tests/RouteBuilderTests.cs)

### Faza 3: Dokumentacja ✅

- [x] Zaktualizować XML docs w `ServiceRouteAttribute`

## Powiązane dokumenty

- [ADR-001: ServiceProxy Architecture](./ADR-001-ServiceProxy-Architecture.md)
