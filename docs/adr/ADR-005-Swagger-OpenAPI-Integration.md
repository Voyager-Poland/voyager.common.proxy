# ADR-005: Integracja Swagger/OpenAPI z Voyager.Common.Proxy.Server

**Status:** Zaakceptowane
**Data:** 2026-01-30
**Autor:** [Do uzupełnienia]

## Problem

Potrzebujemy automatycznej dokumentacji API dla endpointów generowanych przez `MapServiceProxy<T>()`. Dokumentacja powinna:

1. **Automatycznie odkrywać endpointy** - bez ręcznego opisywania każdej metody
2. **Poprawnie rozpakowywać `Result<T>`** - Swagger powinien pokazywać `T`, nie `Result<T>`
3. **Dokumentować kody odpowiedzi** - mapowanie `Result` → HTTP status codes
4. **Opisywać parametry** - route, query, body z poprawnymi typami
5. **Integrować się z istniejącą infrastrukturą** - ASP.NET Core middleware

**Wyzwania specyficzne dla naszej architektury:**

| Aspekt | Problem |
|--------|---------|
| **Result<T>** | Swagger domyślnie pokaże `Result<User>` zamiast `User` |
| **Kody błędów** | Brak informacji o możliwych błędach (404, 400, etc.) |
| **Konwencje** | Brak atrybutów `[ProducesResponseType]` na metodach |
| **Metadane** | Tylko `ServiceProxyEndpointMetadata` dostępne |

## Kontekst techniczny

### Wymaganie wieloplatformowości

Voyager.Common.Proxy.Server wspiera **dwie platformy**:

| Platforma | Target | Middleware |
|-----------|--------|------------|
| **ASP.NET Core** | net6.0, net8.0 | Minimal APIs z `IEndpointRouteBuilder` |
| **OWIN** | net48 | Raw `AppFunc` delegates |

**Kluczowa różnica:**

```
ASP.NET Core                          OWIN (.NET Framework 4.8)
─────────────────────────────────────────────────────────────────
IEndpointRouteBuilder                 AppFunc (raw delegate)
│                                     │
├─ Metadata system                    ├─ Brak systemu metadanych
├─ IApiDescriptionProvider            ├─ Brak discovery API
├─ Swashbuckle.AspNetCore ✓           ├─ Swashbuckle.AspNetCore ✗
└─ NSwag.AspNetCore ✓                 └─ Wymaga własnego rozwiązania
```

**Implikacja:** Rozwiązanie oparte tylko na Swashbuckle.AspNetCore **nie będzie działać z OWIN**.

### Istniejąca infrastruktura Swagger w OWIN

W aplikacjach OWIN używamy już **Swagger.Net** (pakiet `Swagger.Net` v8.5.x):

```csharp
// Swagger.Net ma podobne API do Swashbuckle:
// - IOperationFilter
// - IDocumentFilter (z dostępem do SwaggerDocument - można dodawać paths!)
// - SchemaRegistry
```

**Kluczowa informacja:** `IDocumentFilter.Apply()` otrzymuje `SwaggerDocument` który można modyfikować:
```csharp
void Apply(SwaggerDocument swaggerDoc, SchemaRegistry schemaRegistry, IApiExplorer apiExplorer);
```

To oznacza, że możemy użyć **tego samego podejścia** dla obu platform:
- **ASP.NET Core**: Swashbuckle.AspNetCore + `IDocumentFilter`
- **OWIN**: Swagger.Net + `IDocumentFilter`

### Istniejące metadane

Każdy endpoint ma już attached `ServiceProxyEndpointMetadata` (tylko ASP.NET Core):

```csharp
// ServiceProxyEndpointRouteBuilderExtensions.cs:80-84
builder.WithMetadata(new ServiceProxyEndpointMetadata(
    descriptor.ServiceType,      // typeof(IUserService)
    descriptor.Method,           // MethodInfo dla GetUserAsync
    descriptor.ReturnType,       // typeof(Result<User>)
    descriptor.ResultValueType   // typeof(User)
));
```

### EndpointDescriptor zawiera

- `HttpMethod` - GET, POST, PUT, DELETE
- `RouteTemplate` - "/user-service/get-user/{id}"
- `Parameters` - lista `ParameterDescriptor` z Name, Type, Source (Route/Query/Body)
- `ResultValueType` - rozpakowany typ z `Result<T>`

## Propozycje rozwiązań

---

## Opcja A: Swashbuckle + IOperationFilter (tylko ASP.NET Core)

### Opis

Wykorzystanie najpopularniejszej biblioteki Swagger dla ASP.NET Core (Swashbuckle) z własnym filtrem operacji, który:
- Odczytuje `ServiceProxyEndpointMetadata`
- Podmienia typ odpowiedzi z `Result<T>` na `T`
- Dodaje dokumentację kodów błędów

### Architektura

```
┌─────────────────────────────────────────────────────────────┐
│                    ASP.NET Core Pipeline                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────┐     ┌──────────────────────────────┐ │
│  │   Swashbuckle    │────►│  ServiceProxyOperationFilter │ │
│  │  (swagger.json)  │     │  - Reads metadata            │ │
│  └──────────────────┘     │  - Unwraps Result<T>         │ │
│           │               │  - Adds error responses      │ │
│           ▼               └──────────────────────────────┘ │
│  ┌──────────────────┐                                      │
│  │   Swagger UI     │                                      │
│  │  (/swagger)      │                                      │
│  └──────────────────┘                                      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Implementacja

**Nowy projekt:** `Voyager.Common.Proxy.Server.Swagger`

```csharp
// ServiceProxyOperationFilter.cs
public class ServiceProxyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata
            .OfType<ServiceProxyEndpointMetadata>()
            .FirstOrDefault();

        if (metadata == null) return;

        // 1. Podmień typ odpowiedzi 200 OK
        if (metadata.ResultValueType != null)
        {
            var schema = context.SchemaGenerator.GenerateSchema(
                metadata.ResultValueType,
                context.SchemaRepository);

            operation.Responses["200"] = new OpenApiResponse
            {
                Description = "Success",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = schema }
                }
            };
        }
        else
        {
            operation.Responses["204"] = new OpenApiResponse
            {
                Description = "No Content"
            };
            operation.Responses.Remove("200");
        }

        // 2. Dodaj standardowe kody błędów
        AddErrorResponses(operation);

        // 3. Ustaw OperationId z nazwy metody
        operation.OperationId = metadata.Method.Name.Replace("Async", "");

        // 4. Grupuj po interfejsie
        operation.Tags = new List<OpenApiTag>
        {
            new() { Name = metadata.ServiceType.Name.TrimStart('I') }
        };
    }

    private void AddErrorResponses(OpenApiOperation operation)
    {
        var errorSchema = new OpenApiSchema
        {
            Type = "object",
            Properties = new Dictionary<string, OpenApiSchema>
            {
                ["error"] = new() { Type = "string" }
            }
        };

        operation.Responses["400"] = new OpenApiResponse
        {
            Description = "Validation Error",
            Content = ErrorContent(errorSchema)
        };
        operation.Responses["404"] = new OpenApiResponse
        {
            Description = "Not Found",
            Content = ErrorContent(errorSchema)
        };
        operation.Responses["401"] = new OpenApiResponse
        {
            Description = "Unauthorized"
        };
        operation.Responses["500"] = new OpenApiResponse
        {
            Description = "Server Error",
            Content = ErrorContent(errorSchema)
        };
    }
}
```

**Extension method:**

```csharp
// ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServiceProxySwagger(
        this IServiceCollection services,
        Action<SwaggerGenOptions>? configure = null)
    {
        services.AddSwaggerGen(options =>
        {
            options.OperationFilter<ServiceProxyOperationFilter>();
            options.DocumentFilter<ServiceProxyDocumentFilter>();

            // Ignoruj Result<T> w schematach - używaj tylko T
            options.MapType<Result>(() => new OpenApiSchema { Type = "object" });

            configure?.Invoke(options);
        });

        return services;
    }
}
```

**Użycie:**

```csharp
// Program.cs
builder.Services.AddServiceProxySwagger(options =>
{
    options.SwaggerDoc("v1", new() { Title = "My API", Version = "v1" });
});

app.UseSwagger();
app.UseSwaggerUI();
```

### Zalety

| Zaleta | Opis |
|--------|------|
| **Popularność** | Swashbuckle to de facto standard dla ASP.NET Core |
| **Ekosystem** | Duża społeczność, wiele przykładów, łatwe debugowanie |
| **UI wbudowane** | Swagger UI out-of-the-box |
| **Minimalna praca** | Tylko filtr operacji, reszta działa automatycznie |
| **Rozszerzalność** | Łatwo dodać własne filtry dla specyficznych przypadków |

### Wady

| Wada | Opis |
|------|------|
| **Brak wsparcia OWIN** | Swashbuckle.AspNetCore nie działa z .NET Framework 4.8/OWIN |
| **Zależność** | Dodatkowy pakiet NuGet (Swashbuckle.AspNetCore) |
| **Ograniczona kontrola** | Zależność od wewnętrznej implementacji Swashbuckle |
| **Reflection overhead** | Swashbuckle skanuje assembly przy starcie |

### Zależności

```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="6.5.0" />
```

---

## Opcja B: Rozszerzenie metadanych endpointów + natywny Swashbuckle (tylko ASP.NET Core)

### Opis

Zamiast filtrów, dodaj standardowe metadane ASP.NET Core (`ProducesResponseType`, `Accepts`, etc.) bezpośrednio przy rejestracji endpointów. Swashbuckle automatycznie je odczyta.

### Architektura

```
MapServiceProxy<T>()
        │
        ▼
┌───────────────────────────────────────┐
│  Dla każdego endpointu dodaj:         │
│  - .Produces<T>(200)                  │
│  - .Produces<ErrorResponse>(400)      │
│  - .Produces<ErrorResponse>(404)      │
│  - .Accepts<RequestType>("json")      │
│  - .WithTags("ServiceName")           │
│  - .WithName("OperationId")           │
└───────────────────────────────────────┘
        │
        ▼
    Swashbuckle
  (bez modyfikacji)
```

### Implementacja

**Modyfikacja `ServiceProxyEndpointRouteBuilderExtensions.cs`:**

```csharp
foreach (var descriptor in descriptors)
{
    var builder = endpoints.MapMethods(/* ... */);

    // Dodaj metadane dla Swagger
    AddSwaggerMetadata(builder, descriptor);

    // Istniejący kod...
}

private static void AddSwaggerMetadata(
    IEndpointConventionBuilder builder,
    EndpointDescriptor descriptor)
{
    // Typ odpowiedzi sukcesu
    if (descriptor.ResultValueType != null)
    {
        // Używamy reflection bo Produces<T> wymaga generic
        var producesMethod = typeof(OpenApiRouteHandlerBuilderExtensions)
            .GetMethod("Produces", /* ... */);
        // builder.Produces<T>(200, "application/json");
    }
    else
    {
        builder.Produces(204);
    }

    // Standardowe błędy
    builder.Produces<ErrorResponse>(400);
    builder.Produces<ErrorResponse>(404);
    builder.Produces(401);
    builder.Produces<ErrorResponse>(500);

    // Grupowanie
    builder.WithTags(descriptor.ServiceType.Name.TrimStart('I'));

    // Operation ID
    builder.WithName(descriptor.Method.Name.Replace("Async", ""));

    // Body type dla POST/PUT
    var bodyParam = descriptor.Parameters
        .FirstOrDefault(p => p.Source == ParameterSource.Body);
    if (bodyParam != null)
    {
        // builder.Accepts<T>("application/json");
    }
}
```

### Zalety

| Zaleta | Opis |
|--------|------|
| **Brak dodatkowego pakietu** | Wykorzystuje tylko standardowy Swashbuckle |
| **Natywne metadane** | Zgodne z ASP.NET Core conventions |
| **Przenośność** | Inne generatory OpenAPI też odczytają te metadane |

### Wady

| Wada | Opis |
|------|------|
| **Brak wsparcia OWIN** | Metadane Minimal APIs nie istnieją w OWIN |
| **Skomplikowana implementacja** | Wymaga reflection dla generic `Produces<T>()` |
| **Modyfikacja core** | Zmiana w głównym module Server.AspNetCore |
| **Silne sprzężenie** | Swagger-owe typy w core projekcie |

---

## Opcja C: NSwag zamiast Swashbuckle

### Opis

Alternatywna biblioteka OpenAPI z podobnym podejściem do Opcji A, ale z innymi trade-off'ami.

### Implementacja

```csharp
// NSwag processor
public class ServiceProxyOperationProcessor : IOperationProcessor
{
    public bool Process(OperationProcessorContext context)
    {
        var metadata = context.MethodInfo.DeclaringType?
            .GetCustomAttribute<ServiceProxyEndpointMetadata>();
        // ... podobna logika do Opcji A
        return true;
    }
}
```

### Zalety

| Zaleta | Opis |
|--------|------|
| **Generowanie klientów** | NSwag może generować C#/TypeScript klienty |
| **Aktywny development** | Rico Suter aktywnie rozwija projekt |
| **Lepsza obsługa nullable** | Lepsze wsparcie dla nullable reference types |

### Wady

| Wada | Opis |
|------|------|
| **Mniejsza popularność** | Mniej przykładów i dokumentacji niż Swashbuckle |
| **Inny model rozszerzeń** | IOperationProcessor vs IOperationFilter |
| **Większa złożoność** | Więcej opcji konfiguracji |

---

## Opcja D: Własny generator OpenAPI (wieloplatformowy)

### Opis

Pełna kontrola - własna generacja dokumentu OpenAPI bez zewnętrznych bibliotek. **Działa zarówno z ASP.NET Core jak i OWIN**, bo bazuje na wspólnym `ServiceScanner` i `EndpointDescriptor`.

### Architektura

```
┌─────────────────────────────────────────────────────────────┐
│         Voyager.Common.Proxy.Server.OpenApi                  │
│              (netstandard2.0 - wspólny)                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌──────────────────┐     ┌──────────────────────────────┐ │
│  │ OpenApiGenerator │────►│  ServiceScanner              │ │
│  │                  │     │  (reuse existing)            │ │
│  └──────────────────┘     └──────────────────────────────┘ │
│           │                                                 │
│           ▼                                                 │
│  ┌──────────────────┐                                      │
│  │  OpenApiDocument │                                      │
│  │  (JSON string)   │                                      │
│  └──────────────────┘                                      │
│                                                             │
└─────────────────────────────────────────────────────────────┘
              │                           │
              ▼                           ▼
┌─────────────────────────┐   ┌─────────────────────────┐
│  ASP.NET Core           │   │  OWIN                   │
│  app.MapGet("/openapi", │   │  app.Use(OpenApiMiddle- │
│    () => generator...)  │   │    ware.Create(...))    │
└─────────────────────────┘   └─────────────────────────┘
```

### Implementacja

```csharp
public class OpenApiGenerator
{
    public OpenApiDocument Generate(IEnumerable<EndpointDescriptor> descriptors)
    {
        var document = new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "API", Version = "1.0" },
            Paths = new OpenApiPaths()
        };

        foreach (var descriptor in descriptors)
        {
            var path = GetOrCreatePath(document, descriptor.RouteTemplate);
            var operation = CreateOperation(descriptor);
            path.Operations[MapHttpMethod(descriptor.HttpMethod)] = operation;
        }

        GenerateSchemas(document, descriptors);
        return document;
    }

    private OpenApiOperation CreateOperation(EndpointDescriptor descriptor)
    {
        return new OpenApiOperation
        {
            OperationId = descriptor.Method.Name.Replace("Async", ""),
            Tags = new[] { descriptor.ServiceType.Name.TrimStart('I') },
            Parameters = CreateParameters(descriptor),
            RequestBody = CreateRequestBody(descriptor),
            Responses = CreateResponses(descriptor)
        };
    }
}
```

### Zalety

| Zaleta | Opis |
|--------|------|
| **Wieloplatformowość** | Jeden kod dla ASP.NET Core i OWIN |
| **Zero zależności** | Pełna kontrola, brak zewnętrznych pakietów |
| **Optymalizacja** | Generowanie tylko tego co potrzebne |
| **Spójność** | Dokładnie dopasowane do naszej architektury |
| **netstandard2.0** | Wspólna biblioteka dla wszystkich platform |

### Wady

| Wada | Opis |
|------|------|
| **Dużo pracy** | Implementacja generatora schematów JSON Schema |
| **Utrzymanie** | Śledzenie zmian w specyfikacji OpenAPI |
| **Brak UI** | Trzeba osobno dodać Swagger UI (lub embedded) |
| **Edge cases** | Generyczne typy, nullable, collections, etc. |

### Implementacja wieloplatformowa

```csharp
// Voyager.Common.Proxy.Server.OpenApi (netstandard2.0)
public class OpenApiGenerator
{
    private readonly ServiceScanner _scanner = new();

    public string GenerateJson<TService>() where TService : class
    {
        var endpoints = _scanner.ScanInterface<TService>();
        var document = BuildDocument(endpoints);
        return JsonSerializer.Serialize(document);
    }
}

// ASP.NET Core - użycie
app.MapGet("/openapi.json", () =>
{
    var generator = new OpenApiGenerator();
    return Results.Content(
        generator.GenerateJson<IUserService>(),
        "application/json");
});

// OWIN - użycie
app.Use(OpenApiOwinMiddleware.Create<IUserService>("/openapi.json"));
```

---

## Opcja E: Swashbuckle + Swagger.Net (Rekomendowana dla wieloplatformowości)

### Opis

Wykorzystanie **istniejących bibliotek Swagger** na obu platformach z analogicznymi filtrami:
- **ASP.NET Core**: Swashbuckle.AspNetCore + `IDocumentFilter`
- **OWIN**: Swagger.Net (już używany!) + `IDocumentFilter`

Obie biblioteki mają **prawie identyczne API** dla filtrów, więc logika generowania może być współdzielona.

### Architektura

```
┌────────────────────────────────────────────────────────────────┐
│     Voyager.Common.Proxy.Server.Swagger.Core                   │
│                    (netstandard2.0)                            │
│  ┌────────────────────────────────────────────────────────┐   │
│  │  ServiceProxySwaggerGenerator                          │   │
│  │  - GeneratePathsFromInterface<T>()                     │   │
│  │  - GenerateSchemas()                                    │   │
│  │  - MapResultToResponses()                               │   │
│  │  (używa ServiceScanner z Server.Core)                  │   │
│  └────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────┘
                    ▲                       ▲
                    │                       │
┌───────────────────┴───────┐   ┌──────────┴────────────────────┐
│  .Server.Swagger          │   │  .Server.Swagger.Owin         │
│  (net6.0, net8.0)         │   │  (net48)                      │
│                           │   │                               │
│  Swashbuckle.AspNetCore   │   │  Swagger.Net (już używany)    │
│  ┌─────────────────────┐  │   │  ┌─────────────────────────┐  │
│  │ IDocumentFilter     │  │   │  │ IDocumentFilter         │  │
│  │ - wywołuje Core     │  │   │  │ - wywołuje Core         │  │
│  │ - dodaje paths      │  │   │  │ - dodaje paths          │  │
│  └─────────────────────┘  │   │  └─────────────────────────┘  │
└───────────────────────────┘   └───────────────────────────────┘
```

### Implementacja

**Wspólna logika (netstandard2.0):**

```csharp
// Voyager.Common.Proxy.Server.Swagger.Core
public class ServiceProxySwaggerGenerator
{
    private readonly ServiceScanner _scanner = new();

    public IReadOnlyList<PathDefinition> GeneratePaths<TService>()
        where TService : class
    {
        var endpoints = _scanner.ScanInterface<TService>();
        return endpoints.Select(CreatePathDefinition).ToList();
    }

    private PathDefinition CreatePathDefinition(EndpointDescriptor endpoint)
    {
        return new PathDefinition
        {
            Path = endpoint.RouteTemplate,
            HttpMethod = endpoint.HttpMethod,
            OperationId = endpoint.Method.Name.Replace("Async", ""),
            Tags = new[] { endpoint.ServiceType.Name.TrimStart('I') },
            Parameters = CreateParameters(endpoint),
            RequestBody = CreateRequestBody(endpoint),
            Responses = CreateResponses(endpoint)  // Unwrap Result<T>!
        };
    }
}
```

**Swashbuckle filter (ASP.NET Core):**

```csharp
// Voyager.Common.Proxy.Server.Swagger
public class ServiceProxyDocumentFilter<TService> : IDocumentFilter
    where TService : class
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var generator = new ServiceProxySwaggerGenerator();
        var paths = generator.GeneratePaths<TService>();

        foreach (var path in paths)
        {
            var openApiPath = ConvertToOpenApiPathItem(path, context.SchemaGenerator);
            swaggerDoc.Paths.Add(path.Path, openApiPath);
        }
    }
}

// Użycie:
services.AddSwaggerGen(c =>
{
    c.DocumentFilter<ServiceProxyDocumentFilter<IUserService>>();
    c.DocumentFilter<ServiceProxyDocumentFilter<IOrderService>>();
});
```

**Swagger.Net filter (OWIN):**

```csharp
// Voyager.Common.Proxy.Server.Swagger.Owin
public class ServiceProxyDocumentFilter<TService> : IDocumentFilter
    where TService : class
{
    public void Apply(SwaggerDocument swaggerDoc, SchemaRegistry schemaRegistry, IApiExplorer apiExplorer)
    {
        var generator = new ServiceProxySwaggerGenerator();
        var paths = generator.GeneratePaths<TService>();

        foreach (var path in paths)
        {
            var swaggerPath = ConvertToSwaggerPathItem(path, schemaRegistry);
            swaggerDoc.paths.Add(path.Path, swaggerPath);
        }
    }
}

// Użycie (w SwaggerConfig.cs):
GlobalConfiguration.Configuration
    .EnableSwagger(c =>
    {
        c.DocumentFilter<ServiceProxyDocumentFilter<IUserService>>();
        c.DocumentFilter<ServiceProxyDocumentFilter<IOrderService>>();
    });
```

### Zalety

| Zaleta | Opis |
|--------|------|
| **Wykorzystuje istniejące biblioteki** | Swagger.Net już używany w OWIN, Swashbuckle standard w Core |
| **Wspólna logika** | 90% kodu w `Swagger.Core` (netstandard2.0) |
| **Minimalna praca** | Tylko cienkie adaptery dla każdej platformy |
| **Swagger UI działa** | Obie biblioteki mają wbudowany UI |
| **Spójne API** | Podobna konfiguracja na obu platformach |
| **Brak nowych zależności OWIN** | Swagger.Net już jest w projekcie |

### Wady

| Wada | Opis |
|------|------|
| **Dwa pakiety adapterów** | `.Server.Swagger` i `.Server.Swagger.Owin` |
| **Różnice w API** | Drobne różnice między Swashbuckle a Swagger.Net |
| **Konwersja typów** | Różne modele dokumentu (OpenApiDocument vs SwaggerDocument) |

### Struktura projektu

```
src/
├── Voyager.Common.Proxy.Server.Core/              (istniejący)
├── Voyager.Common.Proxy.Server.Swagger.Core/      (NOWY, netstandard2.0)
│   ├── ServiceProxySwaggerGenerator.cs            # Wspólna logika
│   ├── PathDefinition.cs                          # Model pośredni
│   ├── SchemaGenerator.cs                         # Generowanie schematów
│   └── ResponseMapper.cs                          # Result<T> → responses
├── Voyager.Common.Proxy.Server.Swagger/           (NOWY, net6.0+)
│   ├── ServiceProxyDocumentFilter.cs              # Adapter Swashbuckle
│   └── ServiceCollectionExtensions.cs
└── Voyager.Common.Proxy.Server.Swagger.Owin/      (NOWY, net48)
    ├── ServiceProxyDocumentFilter.cs              # Adapter Swagger.Net
    └── SwaggerConfigExtensions.cs
```

---

## Porównanie opcji

| Kryterium | Opcja A | Opcja B | Opcja C | Opcja D | Opcja E |
|-----------|---------|---------|---------|---------|---------|
| | Swashbuckle | Metadane | NSwag | Własny | **Swashbuckle + Swagger.Net** |
| **ASP.NET Core** | ✅ | ✅ | ✅ | ✅ | ✅ |
| **OWIN (.NET 4.8)** | ❌ | ❌ | ⚠️ częściowo | ✅ | ✅ |
| **Czas implementacji** | 2-3 dni | 3-4 dni | 2-3 dni | 2-3 tyg. | **3-5 dni** |
| **Złożoność** | Niska | Średnia | Niska | Wysoka | **Niska** |
| **Nowe zależności** | 1 pakiet | 0 | 1 pakiet | 0 | **0 dla OWIN** |
| **Utrzymanie** | Niskie | Niskie | Niskie | Wysokie | **Niskie** |
| **Kontrola** | Średnia | Niska | Średnia | Pełna | Średnia |
| **Ekosystem** | Duży | N/A | Średni | Brak | **Duży** |
| **Generowanie klientów** | Zewn. | Zewn. | Wbudowane | Brak | Zewn. |
| **Swagger UI** | Wbudowany | Wbudowany | Wbudowany | Własny | **Wbudowany** |

## Rekomendacja

### ⭐ Rekomendowana: Opcja E (Swashbuckle + Swagger.Net)

**Uzasadnienie:**

1. **Wykorzystuje istniejącą infrastrukturę** - Swagger.Net już jest w projekcie OWIN
2. **Brak nowych zależności dla OWIN** - tylko adaptery do istniejących bibliotek
3. **Spójne podejście** - `IDocumentFilter` na obu platformach
4. **Szybka implementacja** - 3-5 dni zamiast 2-3 tygodni
5. **Sprawdzone biblioteki** - Swashbuckle i Swagger.Net są stabilne i dojrzałe
6. **Wbudowany Swagger UI** - na obu platformach bez dodatkowej pracy

**Struktura projektu:**

```
src/
├── Voyager.Common.Proxy.Server.Core/              (istniejący)
├── Voyager.Common.Proxy.Server.Swagger.Core/      (NOWY, netstandard2.0)
│   ├── ServiceProxySwaggerGenerator.cs            # Wspólna logika
│   ├── PathDefinition.cs                          # Model pośredni
│   └── ResponseMapper.cs                          # Result<T> → responses
├── Voyager.Common.Proxy.Server.Swagger/           (NOWY, net6.0+)
│   ├── ServiceProxyDocumentFilter.cs              # Adapter Swashbuckle
│   └── ServiceCollectionExtensions.cs
└── Voyager.Common.Proxy.Server.Swagger.Owin/      (NOWY, net48)
    ├── ServiceProxyDocumentFilter.cs              # Adapter Swagger.Net
    └── SwaggerConfigExtensions.cs
```

---

### Alternatywa: Opcja A (tylko ASP.NET Core)

Jeśli wsparcie OWIN **nie jest wymagane**, prostsza jest Opcja A:

```
src/
├── Voyager.Common.Proxy.Server.AspNetCore/     (istniejący)
└── Voyager.Common.Proxy.Server.Swagger/        (NOWY)
    ├── ServiceProxyDocumentFilter.cs
    └── ServiceCollectionExtensions.cs
```

---

### Alternatywa: Opcja D (zero zależności)

Jeśli zespół preferuje **pełną kontrolę i zero zewnętrznych zależności**:
- Więcej pracy (2-3 tygodnie)
- Własny Swagger UI (embedded lub CDN)
- Pełna kontrola nad formatem dokumentu

---

### Minimalny zakres implementacji (MVP) - Opcja E

**Faza 1: Core (netstandard2.0)**
- [ ] `ServiceProxySwaggerGenerator` - generowanie paths z `ServiceScanner`
- [ ] `PathDefinition` - model pośredni niezależny od platformy
- [ ] `ResponseMapper` - mapowanie `Result<T>` na responses (200, 400, 404, etc.)
- [ ] `SchemaGenerator` - generowanie JSON Schema dla typów

**Faza 2: Adaptery**
- [ ] `ServiceProxyDocumentFilter` dla Swashbuckle (ASP.NET Core)
- [ ] `ServiceProxyDocumentFilter` dla Swagger.Net (OWIN)
- [ ] Extension methods dla łatwej konfiguracji

**Faza 3: Testy i dokumentacja**
- [ ] Testy jednostkowe dla Core
- [ ] Przykłady użycia dla obu platform
- [ ] Aktualizacja README

### Rozszerzenia (później)

- [ ] Wsparcie dla XML comments z interfejsów
- [ ] Customizacja kodów błędów per metoda (atrybuty)
- [ ] Grouping i versioning
- [ ] Security definitions (JWT, API Key, OAuth2)
- [ ] Wsparcie dla `[Description]` atrybutów

## Pytania do dyskusji

1. **Czy wsparcie OWIN jest wymagane?**
   - Tak → **Opcja E (Swashbuckle + Swagger.Net)** - rekomendowana
   - Nie → Opcja A (tylko Swashbuckle)

2. **Czy preferujemy zero zależności zewnętrznych?**
   - Tak → Opcja D (własny generator)
   - Nie → **Opcja E** (wykorzystuje istniejące biblioteki)

3. **Jaki jest akceptowalny czas implementacji?**
   - 2-3 dni → Opcja A (tylko ASP.NET Core)
   - 3-5 dni → **Opcja E** (obie platformy)
   - 2-3 tygodnie → Opcja D (własny generator)

4. **Czy Swagger.Net jest już skonfigurowany w projekcie OWIN?**
   - Tak → **Opcja E** będzie najszybsza (tylko dodanie `IDocumentFilter`)
   - Nie → rozważyć Opcja D lub A

## Decyzja

**Wybrana opcja:** Opcja E (Swashbuckle + Swagger.Net)

**Uzasadnienie:**

1. **Swagger jest standardem w organizacji** - wszystkie API używają Swagger do dokumentacji
2. **Wsparcie OWIN wymagane** - kilka aplikacji nadal działa na OWIN/.NET Framework 4.8
3. **Programiści frontend** - zespół frontendowy nie zna C#, więc Swagger UI jest dla nich kluczowym narzędziem do integracji z API
4. **Wykorzystanie istniejącej infrastruktury** - Swagger.Net już skonfigurowany w projektach OWIN
5. **Spójne doświadczenie** - ta sama dokumentacja API niezależnie od platformy backendowej

---

**Powiązane dokumenty:**
- [ADR-001: Architektura ServiceProxy](./ADR-001-ServiceProxy-Architecture.md)
- [Swashbuckle.AspNetCore GitHub](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [Swagger.Net GitHub](https://github.com/heldersepu/Swagger-Net)
- [OpenAPI Specification](https://spec.openapis.org/oas/v3.1.0)
