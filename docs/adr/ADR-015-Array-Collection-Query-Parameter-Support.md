# ADR-015: Obsługa tablic i kolekcji jako parametrów query string

**Status:** Etap 1 zaimplementowany (VP0001 analizator + CodeFix)
**Data:** 2026-02-23

## Problem

Framework proxy klasyfikuje typy parametrów binarnie: **simple** (primitive, string, decimal, DateTime, Guid, enum) lub **complex** (wszystko inne). Tablice i kolekcje prostych typów (`int[]`, `List<string>`, `IEnumerable<Guid>`) trafiają do kategorii complex, co powoduje niepoprawne zachowanie na żądaniach GET/DELETE.

### Obecne zachowanie dla `int[] idTickets = [1, 2, 3]` na GET

**Klient** (`RouteBuilder.BuildRequest`) — iteruje po **właściwościach typu** `int[]` poprzez reflection:

```
GET /api/service/method?Length=3&LongLength=3&IsReadOnly=False&IsFixedSize=True&IsSynchronized=False
```

Wartości elementów tablicy (`1, 2, 3`) są **całkowicie utracone**.

**Serwer** (`ParameterBinder.BindFromRouteAndQuery`) — próbuje stworzyć instancję `int[]` i ustawić właściwości z query string. Tablica nie ma konstruktora bezparametrowego ani settable properties — wynik to `null` lub `int[0]`.

### Dotknięte miejsca w kodzie

| Warstwa | Plik | Problem |
|---------|------|---------|
| Client | `RouteBuilder.IsComplexType()` | `int[]` → `true` (complex) |
| Client | `RouteBuilder.BuildRequest()` | Serializuje metadane tablicy zamiast elementów |
| Server | `ServiceScanner.IsComplexType()` | `int[]` → `true` → `ParameterSource.RouteAndQuery` |
| Server | `ParameterBinder.BindFromRouteAndQuery()` | Nie potrafi odtworzyć tablicy z query params |

### Skala problemu

Dotyczy każdej sygnatury GET/DELETE z parametrem tablicowym lub kolekcyjnym:

```csharp
Task<Result<Data>> GetAsync(int[] ids);           // ❌ Cicha utrata danych
Task<Result<Data>> GetAsync(List<string> codes);   // ❌ Cicha utrata danych
Task<Result<Data>> GetAsync(Guid[] refs);          // ❌ Cicha utrata danych
```

Najgroźniejszy aspekt: błąd jest **cichy** — brak wyjątku, brak ostrzeżenia, pozornie poprawne żądanie HTTP.

## Decyzja

Implementacja trzyetapowa:

### Etap 1: Analizator Roslyn — `Voyager.Common.Proxy.Analyzers` (wysoki priorytet)

Nowy pakiet NuGet z analizatorem Roslyn, który waliduje interfejsy proxy **w IDE i podczas kompilacji** — zanim kod dotrze do runtime.

#### Reguły analizatora

| ID | Severity | Opis | Przykład |
|-----|----------|------|----------|
| `VP0001` | **Error** | Tablica/kolekcja jako parametr GET/DELETE | `[HttpGet] GetAsync(int[] ids)` |
| `VP0002` | **Error** | Metoda nie zwraca `Task` | `Result<T> Get()` (brak async) |
| `VP0003` | **Error** | Metoda nie zwraca `Result`/`Result<T>` | `Task<string> GetAsync()` |
| `VP0004` | **Error** | Wiele complex params na POST (body conflict) | `PostAsync(Req a, Req b)` |
| `VP0005` | **Warning** | Brak `[ServiceRoute]` + nietypowa konwencja nazwy | `interface IFoo` (nie pasuje do `I*Service`) |
| `VP0006` | **Error** | `[ProducesContentType]` na nie-`Result<string>` | `[ProducesContentType("text/csv")] Task<Result<int>>` |

#### Struktura pakietu

```
Voyager.Common.Proxy.Analyzers/
├── Voyager.Common.Proxy.Analyzers.csproj   // outputType: Analyzer
├── ProxyInterfaceAnalyzer.cs               // DiagnosticAnalyzer — główna logika
├── DiagnosticDescriptors.cs                // VP0001–VP0006 definicje
└── ProxyInterfaceCodeFixProvider.cs        // CodeFix: VP0001 → zamień na [HttpPost]
```

#### Dystrybucja

Analizator dostarczany jako zależność pakietu `Voyager.Common.Proxy.Abstractions`:

```xml
<!-- Voyager.Common.Proxy.Abstractions.csproj -->
<ItemGroup>
  <ProjectReference Include="..\Voyager.Common.Proxy.Analyzers\Voyager.Common.Proxy.Analyzers.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

Dzięki temu każdy projekt, który referencuje `Abstractions` (czyli definiuje interfejsy proxy), **automatycznie** dostaje analizator — zero dodatkowej konfiguracji.

#### Doświadczenie dewelopera

```csharp
[HttpGet("get-invoice-data")]
Task<Result<InvoiceData>> GetInvoiceDataAsync(int[] idTickets);
//                                            ~~~~~~~~~~~~~~
// VP0001: Parameter 'idTickets' of type 'int[]' is not supported
//         for GET requests. Use [HttpPost] or change to a simple type.
//         [💡 Quick fix: Change to HttpPost]
```

#### Dlaczego analizator, a nie walidacja runtime

| Aspekt | Analizator Roslyn | Runtime walidacja (DI) |
|--------|-------------------|----------------------|
| **Kiedy łapie błąd** | Podczas pisania kodu | Przy starcie aplikacji |
| **Feedback loop** | ~0s, czerwona falka w IDE | ~10-30s, crash w logach |
| **Pokrycie** | Każdy build, CI/CD | Tylko jeśli uruchomisz aplikację |
| **Code fix** | Tak — Quick Action w IDE | Nie |
| **Koszt utrzymania** | Osobny projekt + testy z `CSharpAnalyzerVerifier` | Kilka linii w istniejącym kodzie |

Analizator nie wyklucza dodatkowej walidacji runtime jako defense-in-depth, ale to analizator jest **główną linią obrony**.

### Etap 2: Runtime walidacja jako defense-in-depth (średni priorytet)

Dodatkowa walidacja przy rejestracji serwisu dla scenariuszy, w których analizator został pominięty (np. dynamiczne generowanie proxy, wyłączony analizator):

**Klient** — w `ServiceCollectionExtensions` przy tworzeniu proxy:

```csharp
if (httpMethod is Get or Delete && IsCollectionOfSimpleType(param.ParameterType))
{
    throw new InvalidOperationException(
        $"VP0001: Parameter '{param.Name}' of type '{param.ParameterType.Name}' on method " +
        $"'{method.DeclaringType.Name}.{method.Name}' is not supported for {httpMethod} requests. " +
        $"Use [HttpPost] or wrap the parameter in a request object.");
}
```

**Serwer** — w `ServiceScanner.BuildParameterDescriptors`.

### Etap 3: Pełna obsługa tablic (niski priorytet)

Obsługa `T[]` i `List<T>` (gdzie `T` jest simple type) jako powtarzalnych parametrów query string, zgodnie ze standardem stosowanym przez ASP.NET Core.

**Klient** (`RouteBuilder`):

```csharp
// Nowa kategoria: "collection of simple types"
if (IsCollectionOfSimpleType(param.ParameterType))
{
    // int[] {1, 2, 3} → idTickets=1&idTickets=2&idTickets=3
    foreach (var item in (IEnumerable)value)
    {
        queryParams.Add($"{Uri.EscapeDataString(param.Name!)}={Uri.EscapeDataString(item.ToString()!)}");
    }
}
```

**Serwer** (`ServiceScanner` + `ParameterBinder`):

```csharp
// ServiceScanner: klasyfikacja jako Query (nie RouteAndQuery)
if (IsCollectionOfSimpleType(param.ParameterType))
{
    source = ParameterSource.Query;
}

// ParameterBinder: obsługa wielowartościowych query params
// Wymaga rozszerzenia IRequestContext.QueryParameters z
//   IDictionary<string, string> → IDictionary<string, StringValues>
// lub dodania IRequestContext.GetQueryValues(string name) → string[]
```

### Obsługiwane typy (Etap 2)

| Typ parametru | Serializacja query string |
|---|---|
| `int[]` | `?p=1&p=2&p=3` |
| `List<int>` | `?p=1&p=2&p=3` |
| `IEnumerable<string>` | `?p=a&p=b` |
| `string[]` | `?p=a&p=b` |
| `Guid[]` | `?p=aaa&p=bbb` |
| `List<SomeEnum>` | `?p=Value1&p=Value2` |

Typy **poza zakresem**: `int[][]`, `List<List<int>>`, kolekcje typów złożonych — te nadal powinny wymagać POST z body.

### Definicja helpera

```csharp
private static bool IsCollectionOfSimpleType(Type type)
{
    // T[]
    if (type.IsArray && !IsComplexType(type.GetElementType()!))
        return true;

    // List<T>, IEnumerable<T>, IReadOnlyList<T>, etc.
    if (type.IsGenericType)
    {
        var elementType = type.GetGenericArguments().FirstOrDefault();
        if (elementType != null && !IsComplexType(elementType))
        {
            return typeof(IEnumerable<>).MakeGenericType(elementType)
                .IsAssignableFrom(type);
        }
    }

    return false;
}
```

## Alternatywy

### 1. Tylko dokumentacja i workaround POST

**Odrzucone**, ponieważ cichy błąd runtime jest niedopuszczalny. Minimum to Etap 1 (analizator).

### 2. Tylko runtime walidacja (bez analizatora)

**Odrzucone jako jedyne rozwiązanie**, ponieważ:
- Feedback loop jest za długi (start aplikacji vs. czas pisania kodu)
- Nie działa w CI jeśli nie uruchomisz aplikacji
- Brak Quick Fix w IDE

Runtime walidacja ma sens jako **dodatkowa warstwa** (defense-in-depth), ale nie jako jedyna.

### 3. Serializacja tablicy jako JSON w single query param

```
GET /api/method?idTickets=[1,2,3]
```

**Odrzucone**, ponieważ:
- Niestandardowe — serwery proxy i logi trudniej parsują
- Wymaga URL-encoding, co zaciemnia logi
- Niezgodne z konwencją ASP.NET Core

### 4. Serializacja jako comma-separated string

```
GET /api/method?idTickets=1,2,3
```

**Odrzucone** jako domyślne zachowanie, ponieważ:
- Wymaga custom model bindera po stronie serwera
- Niejednoznaczne dla stringów zawierających przecinki
- Można dodać jako opcjonalny `[CommaSeparated]` atrybut w przyszłości

## Konsekwencje

### Etap 1 (Analizator)
- **Pozytywne**: Błąd widoczny w IDE natychmiast — najkrótszy możliwy feedback loop
- **Pozytywne**: Quick Fix automatycznie proponuje zamianę na `[HttpPost]`
- **Pozytywne**: Otwiera drzwi do walidacji kolejnych reguł (VP0002–VP0006) bez zmian w runtime
- **Pozytywne**: Działa w CI nawet bez uruchamiania aplikacji (`dotnet build` wystarczy)
- **Negatywne**: Nowy projekt do utrzymania (analizator + testy z `CSharpAnalyzerVerifier`)
- **Negatywne**: Analizatory Roslyn mają strome learning curve przy pierwszym kontakcie

### Etap 2 (Runtime walidacja)
- **Pozytywne**: Defense-in-depth — łapie przypadki pominięte przez analizator
- **Pozytywne**: Minimalny koszt implementacji
- **Negatywne**: Istniejący kod z tablicami na GET przestanie się uruchamiać (ale i tak nie działał poprawnie)

### Etap 3 (Pełna obsługa)
- **Pozytywne**: Pełna obsługa popularnego wzorca `?id=1&id=2&id=3`
- **Pozytywne**: Spójność z konwencjami ASP.NET Core
- **Negatywne**: Wymaga rozszerzenia `IRequestContext` o obsługę wielowartościowych query params (breaking change dla implementacji serwera)
- **Negatywne**: Dodatkowa złożoność w `RouteBuilder` i `ParameterBinder`

## Plan wdrożenia

1. **v1.10.0** — Etap 1: Analizator Roslyn (`VP0001`) + testy + Code Fix
2. **v1.10.0** — Etap 2: Runtime walidacja jako fallback
3. **v1.11.0+** — Etap 3: Pełna obsługa tablic (jeśli pojawi się realne zapotrzebowanie)
4. **v1.12.0+** — Rozszerzenie analizatora o reguły VP0002–VP0006
