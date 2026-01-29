# ADR-003: Wsparcie dla .NET Framework 4.8 poprzez Castle.DynamicProxy

**Status:** Proponowane
**Data:** 2026-01-29
**Autor:** [Do uzupełnienia]

## Problem

Biblioteka `Voyager.Common.Proxy.Client` używa `System.Reflection.DispatchProxy` do tworzenia dynamicznych proxy w runtime. Klasa `DispatchProxy` jest dostępna **tylko** w .NET Core/.NET 5+.

**Obecna sytuacja:**
- `Voyager.Common.Proxy.Abstractions` - wspiera net48, net6.0, net8.0 ✅
- `Voyager.Common.Proxy.Client` - wspiera tylko net6.0, net8.0 ❌

**Konsekwencje:**
- Projekty na .NET Framework 4.8 nie mogą używać automatycznego proxy klienta
- Muszą ręcznie implementować komunikację HTTP
- Brak spójności w ekosystemie Voyager

## Decyzja

Wprowadzamy abstrakcję `IProxyFactory` i dwie implementacje:

1. **DispatchProxyFactory** - dla .NET 6.0+ (obecna implementacja)
2. **CastleDynamicProxyFactory** - dla .NET Framework 4.8 (nowa)

### Architektura zgodna z SOLID

```
┌─────────────────────────────────────────────────────────────────┐
│                    ServiceProxy<TService>                        │
│                 (High-level module - Facade)                     │
│                                                                 │
│  Odpowiedzialność: Orkiestracja tworzenia proxy                 │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼ depends on abstraction
┌─────────────────────────────────────────────────────────────────┐
│                      IProxyFactory                               │
│                       (Interface)                                │
│                                                                 │
│  TService CreateProxy<TService>(IMethodInterceptor interceptor) │
└─────────────────────────────────────────────────────────────────┘
                    ▲                       ▲
                    │                       │
       ┌────────────┴────────┐   ┌─────────┴──────────────┐
       │  DispatchProxy      │   │  CastleDynamicProxy    │
       │     Factory         │   │       Factory          │
       │                     │   │                        │
       │  #if NET6_0_OR_     │   │  #if NET48             │
       │     GREATER         │   │                        │
       └─────────────────────┘   └────────────────────────┘
                                            │
                                            ▼ uses
                              ┌─────────────────────────┐
                              │   Castle.Core           │
                              │   (NuGet package)       │
                              └─────────────────────────┘
```

### Zasady SOLID w projekcie

#### S - Single Responsibility Principle

Każda klasa ma jedną odpowiedzialność:

| Klasa | Odpowiedzialność |
|-------|------------------|
| `IProxyFactory` | Tworzenie instancji proxy |
| `IMethodInterceptor` | Przechwytywanie wywołań metod |
| `HttpMethodInterceptor` | Translacja wywołań na HTTP |
| `RouteBuilder` | Budowanie URL z metadanych metody |
| `ResultMapper` | Mapowanie HTTP response → Result<T> |
| `ServiceProxyOptions` | Konfiguracja proxy |

#### O - Open/Closed Principle

- **Otwarte na rozszerzenie**: Nowe implementacje `IProxyFactory` mogą być dodawane (np. dla Mono, Unity)
- **Zamknięte na modyfikację**: Istniejący kod `ServiceProxy`, `HttpMethodInterceptor` nie wymaga zmian

```csharp
// Dodanie nowej implementacji nie wymaga zmiany istniejącego kodu
public class UnityProxyFactory : IProxyFactory
{
    // Implementacja dla Unity Runtime
}
```

#### L - Liskov Substitution Principle

Obie implementacje `IProxyFactory` są w pełni wymienne:

```csharp
IProxyFactory factory = new DispatchProxyFactory();
// lub
IProxyFactory factory = new CastleDynamicProxyFactory();

// Użycie identyczne - zachowanie identyczne
var proxy = factory.CreateProxy<IUserService>(interceptor);
```

#### I - Interface Segregation Principle

Małe, wyspecjalizowane interfejsy:

```csharp
// Tylko tworzenie proxy
public interface IProxyFactory
{
    TService CreateProxy<TService>(IMethodInterceptor interceptor)
        where TService : class;
}

// Tylko interceptowanie wywołań
public interface IMethodInterceptor
{
    object? Intercept(MethodInfo method, object?[] arguments);
}
```

Zamiast jednego dużego interfejsu:

```csharp
// ❌ ZŁE - narusza ISP
public interface IProxyService
{
    TService CreateProxy<TService>(...);
    object? Intercept(MethodInfo method, object?[] args);
    string BuildRoute(MethodInfo method);
    Result MapResponse(HttpResponseMessage response);
}
```

#### D - Dependency Inversion Principle

High-level modules zależą od abstrakcji:

```csharp
// ServiceProxy zależy od IProxyFactory, nie od konkretnej implementacji
public static class ServiceProxy<TService> where TService : class
{
    public static TService Create(HttpClient httpClient, ServiceProxyOptions options)
    {
        IProxyFactory factory = ProxyFactoryProvider.GetFactory();
        IMethodInterceptor interceptor = new HttpMethodInterceptor(httpClient, options);

        return factory.CreateProxy<TService>(interceptor);
    }
}
```

## Struktura plików

```
src/Voyager.Common.Proxy.Client/
├── ServiceProxy.cs                    # Fasada (bez zmian w API)
├── ServiceProxyOptions.cs
├── ServiceCollectionExtensions.cs
│
├── Abstractions/                      # Nowy folder
│   ├── IProxyFactory.cs               # Abstrakcja fabryki
│   └── IMethodInterceptor.cs          # Abstrakcja interceptora
│
├── Internal/
│   ├── RouteBuilder.cs                # Bez zmian
│   ├── ResultMapper.cs                # Bez zmian
│   ├── HttpMethodInterceptor.cs       # Nowa klasa (wydzielona z ServiceProxy)
│   │
│   ├── ProxyFactoryProvider.cs        # Wybór implementacji (kompilacja warunkowa)
│   │
│   ├── DispatchProxy/                 # net6.0+
│   │   ├── DispatchProxyFactory.cs
│   │   └── DispatchProxyWrapper.cs
│   │
│   └── CastleProxy/                   # net48
│       ├── CastleDynamicProxyFactory.cs
│       └── CastleInterceptorAdapter.cs
│
└── Voyager.Common.Proxy.Client.csproj
```

## Implementacja

### Faza 1: Refaktoryzacja (SOLID preparation)

1. **Wydzielenie `IMethodInterceptor`**
   - Ekstrakcja logiki interceptowania z `ServiceProxy` do `HttpMethodInterceptor`
   - Interfejs `IMethodInterceptor` z metodą `Intercept(MethodInfo, object[])`

2. **Wydzielenie `IProxyFactory`**
   - Interfejs z metodą `CreateProxy<TService>(IMethodInterceptor)`
   - `DispatchProxyFactory` jako implementacja dla .NET 6.0+

3. **`ProxyFactoryProvider`**
   - Statyczna klasa wybierająca implementację na podstawie platformy
   - Kompilacja warunkowa `#if NET48` / `#if NET6_0_OR_GREATER`

### Faza 2: Castle.DynamicProxy

4. **Dodanie Castle.Core**
   - Warunkowa zależność tylko dla net48
   - `<PackageReference Include="Castle.Core" Version="5.1.1" Condition="'$(TargetFramework)' == 'net48'" />`

5. **`CastleDynamicProxyFactory`**
   - Implementacja `IProxyFactory` używająca `ProxyGenerator` z Castle
   - Adapter `IMethodInterceptor` → Castle `IInterceptor`

6. **Testy**
   - Testy jednostkowe dla obu implementacji
   - Testy integracyjne weryfikujące identyczne zachowanie

### Faza 3: Walidacja

7. **Smoke testy na net48**
   - Projekt testowy targetujący net48
   - Weryfikacja działania end-to-end

8. **Aktualizacja dokumentacji**
   - README z informacją o wsparciu net48
   - Przykłady użycia

## Zależności

**net6.0 / net8.0:**
```
Voyager.Common.Proxy.Client
├── Voyager.Common.Results
├── Voyager.Common.Proxy.Abstractions
├── Microsoft.Extensions.Http
└── System.Text.Json
```

**net48 (dodatkowe):**
```
Voyager.Common.Proxy.Client
├── ... (jak wyżej)
└── Castle.Core (5.1.1)       # ~200KB, brak transitive dependencies
```

## Ryzyka i mitigacje

| Ryzyko | Prawdopodobieństwo | Impact | Mitigacja |
|--------|-------------------|--------|-----------|
| Różnice w zachowaniu proxy | Średnie | Wysoki | Wspólne testy dla obu implementacji |
| Castle.Core breaking changes | Niskie | Średni | Pinowanie wersji; abstrakcja izoluje |
| Większy rozmiar pakietu na net48 | Pewne | Niski | Castle.Core ~200KB - akceptowalne |
| Performance Castle vs DispatchProxy | Niskie | Niski | Benchmark; cache proxy instances |

## Alternatywy

### Alternatywa 1: RealProxy (.NET Framework)

Użycie `System.Runtime.Remoting.Proxies.RealProxy` zamiast Castle.

**Odrzucona ponieważ:**
- Wymaga dziedziczenia po `MarshalByRefObject` - inwazyjne dla interfejsów
- Starsze API, trudniejsze w użyciu
- Castle jest standardem de facto

### Alternatywa 2: Brak wsparcia dla net48

Pozostawienie obecnego stanu - tylko net6.0+.

**Odrzucona ponieważ:**
- Wiele projektów enterprise nadal na .NET Framework
- Brak spójności w ekosystemie Voyager
- Użytkownicy muszą pisać własny kod HTTP

### Alternatywa 3: Source Generator

Generowanie kodu proxy w compile-time zamiast runtime.

**Odrzucona ponieważ:**
- Znacznie większa złożoność implementacji
- Gorsze wsparcie IDE dla generowanego kodu
- Rozważyć jako optymalizację w przyszłości

## Metryki sukcesu

- [ ] Wszystkie istniejące testy przechodzą na net6.0/net8.0
- [ ] Nowe testy przechodzą na net48
- [ ] API publiczne bez breaking changes
- [ ] Performance regression < 10%
- [ ] Rozmiar pakietu net48 < 500KB (z Castle.Core)

---

**Powiązane dokumenty:**
- [ADR-001: ServiceProxy Architecture](./ADR-001-ServiceProxy-Architecture.md)
- [Castle.DynamicProxy](https://www.castleproject.org/projects/dynamicproxy/)
- [DispatchProxy Class](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy)
