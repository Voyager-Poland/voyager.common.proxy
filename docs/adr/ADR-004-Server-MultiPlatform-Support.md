# ADR-004: Wsparcie serwera dla .NET Framework 4.8 i .NET 8.0

**Status:** Proponowane
**Data:** 2026-01-29
**Autor:** [Do uzupełnienia]

## Problem

Biblioteka `Voyager.Common.Proxy.Server` ma automatycznie generować endpointy HTTP na podstawie interfejsów serwisowych. Problem polega na tym, że **ASP.NET Core** (.NET 6.0+) i **ASP.NET Framework** (.NET 4.8) mają całkowicie różne modele programistyczne:

| Aspekt | .NET Framework 4.8 | .NET 6.0+ |
|--------|-------------------|-----------|
| Framework HTTP | ASP.NET Web API 2 / OWIN | ASP.NET Core |
| Pipeline | `IAppBuilder` (OWIN) | `IApplicationBuilder` |
| Rejestracja tras | `HttpConfiguration.Routes` | `IEndpointRouteBuilder` |
| Kontekst żądania | `HttpRequestMessage` | `HttpContext` |
| DI Container | Zewnętrzny (Unity, Autofac) | Wbudowany `IServiceProvider` |
| Hosting | IIS / Self-host (OWIN) | Kestrel / IIS |

**Jeden projekt z multi-targetingiem nie jest możliwy** - API są zbyt różne, a kompilacja warunkowa prowadziłaby do nieczytelnego kodu.

## Decyzja

Rozdzielamy serwer na **4 pakiety NuGet** z jasnym podziałem odpowiedzialności:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                    Voyager.Common.Proxy.Server                          │
│                      (Meta-package / Docs)                              │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
            ┌───────────────────────┼───────────────────────┐
            ▼                       ▼                       ▼
┌─────────────────────┐ ┌─────────────────────┐ ┌─────────────────────────┐
│   .Abstractions     │ │       .Core         │ │     Platform Impl.      │
│   (netstandard2.0)  │ │   (netstandard2.0)  │ │                         │
│                     │ │                     │ │  ┌─────────────────────┐│
│ • IServiceEndpoint  │ │ • ServiceScanner    │ │  │   .AspNetCore       ││
│   Generator         │ │ • EndpointMetadata  │ │  │   (net6.0; net8.0)  ││
│ • IRequestContext   │ │   Builder           │ │  │                     ││
│ • IResponseWriter   │ │ • RequestDispatcher │ │  │ • MapServiceProxy() ││
│ • EndpointDescriptor│ │ • MethodInvoker     │ │  │ • EndpointRouting   ││
│                     │ │                     │ │  └─────────────────────┘│
│                     │ │                     │ │  ┌─────────────────────┐│
│                     │ │                     │ │  │      .Owin          ││
│                     │ │                     │ │  │      (net48)        ││
│                     │ │                     │ │  │                     ││
│                     │ │                     │ │  │ • UseServiceProxy() ││
│                     │ │                     │ │  │ • OWIN Middleware   ││
│                     │ │                     │ │  └─────────────────────┘│
└─────────────────────┘ └─────────────────────┘ └─────────────────────────┘
```

### Pakiety

| Pakiet | Target Framework | Odpowiedzialność |
|--------|------------------|------------------|
| `Voyager.Common.Proxy.Server.Abstractions` | netstandard2.0 | Interfejsy i kontrakty |
| `Voyager.Common.Proxy.Server.Core` | netstandard2.0 | Logika skanowania interfejsów i dispatchowania |
| `Voyager.Common.Proxy.Server.AspNetCore` | net6.0; net8.0 | Integracja z ASP.NET Core |
| `Voyager.Common.Proxy.Server.Owin` | net48 | Integracja z OWIN/Katana |

## Architektura zgodna z SOLID

### S - Single Responsibility Principle

Każdy pakiet i klasa ma jedną, jasno zdefiniowaną odpowiedzialność:

```
┌───────────────────────────────────────────────────────────────────────┐
│ Warstwa                    │ Odpowiedzialność                         │
├───────────────────────────────────────────────────────────────────────┤
│ Abstractions               │ Definicje kontraktów                     │
│ Core/ServiceScanner        │ Skanowanie interfejsów i metod           │
│ Core/EndpointMetadataBuilder│ Budowanie metadanych tras               │
│ Core/RequestDispatcher     │ Wywoływanie metod serwisu                │
│ Core/MethodInvoker         │ Deserializacja args + wywołanie + Result │
│ AspNetCore/ServiceEndpoint │ Adapter dla ASP.NET Core endpoints       │
│ Owin/ServiceMiddleware     │ Adapter dla OWIN middleware              │
└───────────────────────────────────────────────────────────────────────┘
```

### O - Open/Closed Principle

Nowe platformy mogą być dodane bez modyfikacji istniejącego kodu:

```csharp
// Przyszłe rozszerzenia - bez zmian w Core
Voyager.Common.Proxy.Server.Grpc        // gRPC endpoints
Voyager.Common.Proxy.Server.Functions   // Azure Functions
Voyager.Common.Proxy.Server.Lambda      // AWS Lambda
```

### L - Liskov Substitution Principle

Abstrakcje `IRequestContext` i `IResponseWriter` są wymienne między platformami:

```csharp
// Core nie wie, czy działa na ASP.NET Core czy OWIN
public class RequestDispatcher
{
    public async Task DispatchAsync(
        IRequestContext context,      // Może być AspNetCoreRequestContext lub OwinRequestContext
        IResponseWriter response,     // Może być AspNetCoreResponseWriter lub OwinResponseWriter
        EndpointDescriptor endpoint)
    {
        // Identyczna logika niezależnie od platformy
    }
}
```

### I - Interface Segregation Principle

Małe, wyspecjalizowane interfejsy:

```csharp
// Tylko odczyt żądania
public interface IRequestContext
{
    string HttpMethod { get; }
    string Path { get; }
    IReadOnlyDictionary<string, string> RouteValues { get; }
    IReadOnlyDictionary<string, string> QueryParameters { get; }
    Stream Body { get; }
    CancellationToken CancellationToken { get; }
}

// Tylko zapis odpowiedzi
public interface IResponseWriter
{
    Task WriteJsonAsync<T>(T value, int statusCode);
    Task WriteErrorAsync(Error error);
    Task WriteNoContentAsync();
}

// Tylko rejestracja endpointów
public interface IServiceEndpointRegistrar
{
    void RegisterEndpoint(EndpointDescriptor descriptor, RequestDelegate handler);
}
```

### D - Dependency Inversion Principle

High-level modules (Core) zależą od abstrakcji, nie od szczegółów platformy:

```csharp
// Core zależy tylko od abstrakcji
public class RequestDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public async Task DispatchAsync(
        IRequestContext context,      // Abstrakcja
        IResponseWriter response,     // Abstrakcja
        EndpointDescriptor endpoint)
    {
        var service = _serviceProvider.GetRequiredService(endpoint.ServiceType);
        var result = await InvokeMethodAsync(service, endpoint, context);
        await WriteResultAsync(response, result);
    }
}
```

## Struktura plików

```
src/
├── Voyager.Common.Proxy.Server.Abstractions/
│   ├── IRequestContext.cs
│   ├── IResponseWriter.cs
│   ├── IServiceEndpointRegistrar.cs
│   ├── EndpointDescriptor.cs
│   └── Voyager.Common.Proxy.Server.Abstractions.csproj
│
├── Voyager.Common.Proxy.Server.Core/
│   ├── ServiceScanner.cs
│   ├── EndpointMetadataBuilder.cs
│   ├── RequestDispatcher.cs
│   ├── MethodInvoker.cs
│   ├── ParameterBinder.cs
│   └── Voyager.Common.Proxy.Server.Core.csproj
│
├── Voyager.Common.Proxy.Server.AspNetCore/
│   ├── ServiceProxyEndpointRouteBuilderExtensions.cs
│   ├── AspNetCoreRequestContext.cs
│   ├── AspNetCoreResponseWriter.cs
│   ├── ServiceCollectionExtensions.cs
│   └── Voyager.Common.Proxy.Server.AspNetCore.csproj
│
└── Voyager.Common.Proxy.Server.Owin/
    ├── ServiceProxyAppBuilderExtensions.cs
    ├── OwinRequestContext.cs
    ├── OwinResponseWriter.cs
    ├── ServiceProxyMiddleware.cs
    └── Voyager.Common.Proxy.Server.Owin.csproj
```

## Implementacja

### Faza 1: Abstractions (netstandard2.0)

```csharp
// EndpointDescriptor.cs
public sealed class EndpointDescriptor
{
    public Type ServiceType { get; init; }
    public MethodInfo Method { get; init; }
    public string HttpMethod { get; init; }       // GET, POST, PUT, DELETE
    public string RouteTemplate { get; init; }    // "/api/users/{id}"
    public ParameterDescriptor[] Parameters { get; init; }
    public Type ReturnType { get; init; }         // Result<User>, Result
}

public sealed class ParameterDescriptor
{
    public string Name { get; init; }
    public Type Type { get; init; }
    public ParameterSource Source { get; init; }  // Route, Query, Body
    public bool IsOptional { get; init; }
    public object? DefaultValue { get; init; }
}

public enum ParameterSource { Route, Query, Body, CancellationToken }
```

### Faza 2: Core (netstandard2.0)

```csharp
// ServiceScanner.cs
public class ServiceScanner
{
    public IReadOnlyList<EndpointDescriptor> ScanInterface<TService>()
        where TService : class
    {
        // Skanuje metody interfejsu
        // Buduje EndpointDescriptor dla każdej metody
        // Używa tych samych konwencji co Client (RouteBuilder logic)
    }
}

// RequestDispatcher.cs
public class RequestDispatcher
{
    public async Task<object?> DispatchAsync(
        IRequestContext context,
        EndpointDescriptor endpoint,
        object serviceInstance)
    {
        // 1. Bind parameters from context
        // 2. Invoke method
        // 3. Return result (or handle exceptions)
    }
}
```

### Faza 3: AspNetCore (net6.0; net8.0)

```csharp
// ServiceProxyEndpointRouteBuilderExtensions.cs
public static class ServiceProxyEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapServiceProxy<TService>(
        this IEndpointRouteBuilder endpoints)
        where TService : class
    {
        var scanner = new ServiceScanner();
        var descriptors = scanner.ScanInterface<TService>();

        foreach (var descriptor in descriptors)
        {
            endpoints.MapMethods(
                descriptor.RouteTemplate,
                new[] { descriptor.HttpMethod },
                async context =>
                {
                    var requestContext = new AspNetCoreRequestContext(context);
                    var responseWriter = new AspNetCoreResponseWriter(context.Response);
                    var service = context.RequestServices.GetRequiredService<TService>();

                    var dispatcher = new RequestDispatcher();
                    await dispatcher.DispatchAsync(requestContext, responseWriter, descriptor, service);
                });
        }

        return endpoints;
    }
}
```

**Przykład użycia:**

```csharp
// Program.cs (.NET 8.0)
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

app.MapServiceProxy<IUserService>();  // Automatycznie generuje endpointy

app.Run();
```

### Faza 4: Owin (net48)

```csharp
// ServiceProxyAppBuilderExtensions.cs
public static class ServiceProxyAppBuilderExtensions
{
    public static IAppBuilder UseServiceProxy<TService>(
        this IAppBuilder app,
        Func<TService> serviceFactory)
        where TService : class
    {
        var scanner = new ServiceScanner();
        var descriptors = scanner.ScanInterface<TService>();

        return app.Use<ServiceProxyMiddleware<TService>>(descriptors, serviceFactory);
    }
}

// ServiceProxyMiddleware.cs
public class ServiceProxyMiddleware<TService> : OwinMiddleware
    where TService : class
{
    private readonly IReadOnlyList<EndpointDescriptor> _endpoints;
    private readonly Func<TService> _serviceFactory;

    public override async Task Invoke(IOwinContext context)
    {
        var endpoint = MatchEndpoint(context.Request);
        if (endpoint == null)
        {
            await Next.Invoke(context);
            return;
        }

        var requestContext = new OwinRequestContext(context);
        var responseWriter = new OwinResponseWriter(context.Response);
        var service = _serviceFactory();

        var dispatcher = new RequestDispatcher();
        await dispatcher.DispatchAsync(requestContext, responseWriter, endpoint, service);
    }
}
```

**Przykład użycia:**

```csharp
// Startup.cs (.NET Framework 4.8 + OWIN)
public class Startup
{
    public void Configuration(IAppBuilder app)
    {
        var container = new UnityContainer();
        container.RegisterType<IUserService, UserService>();

        app.UseServiceProxy<IUserService>(() => container.Resolve<IUserService>());
    }
}
```

## Zależności

### Voyager.Common.Proxy.Server.Abstractions
```
netstandard2.0
└── (brak zewnętrznych zależności)
```

### Voyager.Common.Proxy.Server.Core
```
netstandard2.0
├── Voyager.Common.Proxy.Server.Abstractions
├── Voyager.Common.Proxy.Abstractions
├── Voyager.Common.Results
└── System.Text.Json (dla net48 compatibility)
```

### Voyager.Common.Proxy.Server.AspNetCore
```
net6.0; net8.0
├── Voyager.Common.Proxy.Server.Core
└── Microsoft.AspNetCore.Routing (framework reference)
```

### Voyager.Common.Proxy.Server.Owin
```
net48
├── Voyager.Common.Proxy.Server.Core
├── Microsoft.Owin (4.2.2)
└── Owin (1.0.0)
```

## Plan implementacji

### Faza 1: Abstractions + Core (2-3 dni)

1. **Abstractions** - interfejsy i kontrakty
2. **ServiceScanner** - skanowanie interfejsów (współdzielenie logiki z Client/RouteBuilder)
3. **EndpointMetadataBuilder** - budowanie metadanych tras
4. **ParameterBinder** - wiązanie parametrów z żądania
5. **RequestDispatcher** - wywoływanie metod serwisu
6. **Unit testy** dla Core

### Faza 2: AspNetCore (1-2 dni)

1. **AspNetCoreRequestContext** - adapter HttpContext → IRequestContext
2. **AspNetCoreResponseWriter** - adapter HttpResponse → IResponseWriter
3. **MapServiceProxy<T>()** - extension method dla IEndpointRouteBuilder
4. **ServiceCollectionExtensions** - rejestracja w DI
5. **Integration testy** z TestServer

### Faza 3: Owin (1-2 dni)

1. **OwinRequestContext** - adapter IOwinContext → IRequestContext
2. **OwinResponseWriter** - adapter IOwinResponse → IResponseWriter
3. **ServiceProxyMiddleware** - OWIN middleware
4. **UseServiceProxy<T>()** - extension method dla IAppBuilder
5. **Integration testy** z OWIN TestServer

### Faza 4: Dokumentacja i przykłady (1 dzień)

1. README dla każdego pakietu
2. Przykładowe projekty (ASP.NET Core + OWIN)
3. Aktualizacja głównego README

## Ryzyka i mitigacje

| Ryzyko | Prawdopodobieństwo | Impact | Mitigacja |
|--------|-------------------|--------|-----------|
| Różnice w zachowaniu routingu | Średnie | Wysoki | Wspólne testy kontraktowe |
| Brak OWIN middleware support w hostach | Niskie | Wysoki | Dokumentacja wymagań |
| Performance differences | Niskie | Średni | Benchmarki; optymalizacja hot paths |
| Complexity w ParameterBinder | Średnie | Średni | Dobre unit testy; edge cases |

## Alternatywy rozważane

### Alternatywa 1: Jeden pakiet z #if directives

```csharp
#if NETFRAMEWORK
    app.UseServiceProxy<IUserService>();  // OWIN
#else
    app.MapServiceProxy<IUserService>();  // ASP.NET Core
#endif
```

**Odrzucona ponieważ:**
- Nieczytelny kod z wieloma #if
- Użytkownik musi instalować niepotrzebne zależności
- Trudne testowanie

### Alternatywa 2: Source Generators

Generowanie kodu endpointów w compile-time.

**Odrzucona ponieważ:**
- Znaczna złożoność implementacji
- Wymaga osobnych generatorów dla każdej platformy
- Rozważyć jako przyszłą optymalizację

### Alternatywa 3: Tylko ASP.NET Core

Brak wsparcia dla .NET Framework 4.8.

**Odrzucona ponieważ:**
- Brak symetrii z Client (który wspiera net48)
- Wiele projektów enterprise nadal na .NET Framework
- Utrudniona migracja

## Metryki sukcesu

- [ ] Wszystkie endpointy generowane zgodnie z konwencjami Client
- [ ] Testy integracyjne Client ↔ Server przechodzą na obu platformach
- [ ] API publiczne spójne między AspNetCore i Owin
- [ ] Dokumentacja i przykłady dla obu platform
- [ ] Performance: < 1ms overhead na request (bez I/O)

---

**Powiązane dokumenty:**
- [ADR-001: ServiceProxy Architecture](./ADR-001-ServiceProxy-Architecture.md)
- [ADR-003: .NET Framework 4.8 Support (Client)](./ADR-003-NetFramework48-Support.md)
- [OWIN Specification](http://owin.org/spec/spec/owin-1.0.0.html)
- [ASP.NET Core Endpoint Routing](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing)
