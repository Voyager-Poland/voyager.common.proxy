# ADR-001: Architektura Voyager.Common.Proxy - Automatyczne mapowanie interfejsów na HTTP REST

**Status:** Zaakceptowane
**Data:** 2026-01-28
**Autor:** [Do uzupełnienia]

## Problem

Potrzebujemy rozwiązania do automatycznej komunikacji HTTP między aplikacjami w ekosystemie Voyager, które:

1. **Współdzieli kontrakt** - ten sam interfejs C# używany po stronie serwera i klienta
2. **Obsługuje `Result<T>`** - nasz standardowy pattern dla wyników operacji z `Voyager.Common.Results`
3. **Minimalizuje boilerplate** - brak ręcznego pisania kontrolerów i klientów HTTP
4. **Zachowuje czystość kontraktów** - interfejsy bez zależności od frameworków HTTP

**Istniejące rozwiązania i dlaczego nie pasują:**

| Rozwiązanie | Problem |
|-------------|---------|
| **Refit** | Wymaga atrybutów `[Get]`, `[Post]` na interfejsie - zależność w kontraktach |
| **RestEase** | Podobnie jak Refit - atrybuty wymagane |
| **gRPC** | Binarny protokół, nie REST/JSON; wymaga .proto files |
| **NSwag/OpenAPI** | Generuje z opisu API, nie z interfejsu C# |
| **ServiceStack** | Komercyjna licencja, ciężki framework |

**Kluczowy problem z Refit:**

Refit wymaga atrybutów na interfejsie:
```csharp
// ❌ Interfejs ma zależność od Refit
public interface IUserService
{
    [Get("/users/{id}")]  // using Refit;
    Task<Result<User>> GetUserAsync(int id);
}
```

Chcemy:
```csharp
// ✅ Czysty interfejs - zero zależności
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id);
}
```

**Dodatkowy problem z Refit i `Result<T>`:**

Refit domyślnie mapuje HTTP status codes na exceptions:
- HTTP 2xx → deserializuje do `T`
- HTTP 4xx/5xx → rzuca `ApiException`

Nasze `Result<T>` ma własne stany błędów (`NotFound`, `ValidationError`, `Forbidden`, etc.), które chcemy mapować na właściwe kody HTTP. Z Refit wymagałoby to:
- Użycia `ApiResponse<Result<T>>` zamiast `Result<T>` (brzydkie API)
- Lub custom `DelegatingHandler` bez dostępu do typu `T` (brak type-safety)

## Decyzja

Tworzymy własną bibliotekę **Voyager.Common.Proxy** składającą się z trzech modułów:

### 1. Architektura modułów

```
┌─────────────────────────────────────────────────────────────┐
│              Voyager.Common.Contracts                        │
│  (lub istniejąca biblioteka z interfejsami)                 │
│                                                             │
│  - IUserService.cs (czysty interfejs)                       │
│  - User.cs, CreateUserRequest.cs (POCO)                     │
│  - Referencja do Voyager.Common.Results                     │
│  - ZERO zależności od Proxy                                 │
└─────────────────────────────────────────────────────────────┘
                    ▲                       ▲
                    │                       │
┌───────────────────┴───────┐   ┌──────────┴────────────────┐
│  Voyager.Common.Proxy     │   │  Voyager.Common.Proxy     │
│         .Server           │   │         .Client           │
│                           │   │                           │
│  - MapServiceEndpoints<T> │   │  - AddServiceProxy<T>     │
│  - Minimal API generation │   │  - DispatchProxy + HTTP   │
│  - Result → HTTP mapping  │   │  - HTTP → Result mapping  │
└───────────────────────────┘   └───────────────────────────┘
          │                                   │
          ▼                                   ▼
┌─────────────────────┐           ┌─────────────────────┐
│      SERWER         │   HTTP    │      KLIENT         │
│                     │◄─────────►│                     │
│  UserService :      │   REST    │  IUserService proxy │
│    IUserService     │   JSON    │                     │
└─────────────────────┘           └─────────────────────┘
```

### 2. Konwencja nazewnictwa metod → HTTP

Mapowanie oparte na prefiksie nazwy metody (convention over configuration):

| Prefix metody | HTTP Verb | Przykład | Endpoint |
|---------------|-----------|----------|----------|
| `Get*` | GET | `GetUserAsync(int id)` | `GET /user?id=123` |
| `Find*` | GET | `FindUsersAsync(string query)` | `GET /users?query=abc` |
| `List*` | GET | `ListOrdersAsync()` | `GET /orders` |
| `Create*` | POST | `CreateUserAsync(User user)` | `POST /user` + body |
| `Add*` | POST | `AddCommentAsync(Comment c)` | `POST /comment` + body |
| `Update*` | PUT | `UpdateUserAsync(User user)` | `PUT /user` + body |
| `Delete*` | DELETE | `DeleteUserAsync(int id)` | `DELETE /user?id=123` |
| `Remove*` | DELETE | `RemoveItemAsync(int id)` | `DELETE /item?id=123` |
| `*` (inne) | POST | `ProcessOrderAsync(Order o)` | `POST /process-order` + body |

**Reguły mapowania parametrów:**
- Typy proste (`int`, `string`, `Guid`, etc.) → query string
- Typy złożone (klasy, rekordy) → JSON body
- `CancellationToken` → ignorowany (obsługiwany przez HTTP timeout)

### 3. Mapowanie `Result<T>` ↔ HTTP Status Codes

**Serwer (Result → HTTP):**

| Result | HTTP Status | Response Body |
|--------|-------------|---------------|
| `Result.Ok(data)` | 200 OK | `data` (JSON) |
| `Result.Ok()` (void) | 204 No Content | - |
| `Result.Created(data)` | 201 Created | `data` (JSON) |
| `Result.NotFound()` | 404 Not Found | `{ "error": "..." }` |
| `Result.ValidationError(errors)` | 400 Bad Request | `{ "errors": [...] }` |
| `Result.Unauthorized()` | 401 Unauthorized | `{ "error": "..." }` |
| `Result.Forbidden()` | 403 Forbidden | `{ "error": "..." }` |
| `Result.Conflict()` | 409 Conflict | `{ "error": "..." }` |
| `Result.ServerError()` | 500 Internal Server Error | `{ "error": "..." }` |

**Klient (HTTP → Result):**

| HTTP Status | Result |
|-------------|--------|
| 200 OK | `Result.Ok(deserializedBody)` |
| 201 Created | `Result.Created(deserializedBody)` |
| 204 No Content | `Result.Ok()` |
| 400 Bad Request | `Result.ValidationError(errorsFromBody)` |
| 401 Unauthorized | `Result.Unauthorized()` |
| 403 Forbidden | `Result.Forbidden()` |
| 404 Not Found | `Result.NotFound()` |
| 409 Conflict | `Result.Conflict()` |
| 5xx | `Result.ServerError(message)` |

### 4. API użycia

**Serwer (Minimal API):**

```csharp
// Implementacja biznesowa - czysta, bez HTTP
public class UserService : IUserService
{
    private readonly IUserRepository _repository;

    public async Task<Result<User>> GetUserAsync(int id)
    {
        var user = await _repository.FindAsync(id);
        return user is null
            ? Result<User>.NotFound($"User {id} not found")
            : Result.Ok(user);
    }

    public async Task<Result<User>> CreateUserAsync(CreateUserRequest request)
    {
        if (string.IsNullOrEmpty(request.Email))
            return Result<User>.ValidationError("Email is required");

        var user = new User { Email = request.Email };
        await _repository.AddAsync(user);
        return Result.Created(user);
    }
}

// Program.cs
builder.Services.AddScoped<IUserService, UserService>();

app.MapServiceEndpoints<IUserService>();
// Automatycznie generuje:
// GET  /user-service/get-user?id={id}
// POST /user-service/create-user (body: CreateUserRequest)
```

**Klient:**

```csharp
// Program.cs
builder.Services.AddServiceProxy<IUserService>(options =>
{
    options.BaseUrl = "https://api.example.com";
    options.Timeout = TimeSpan.FromSeconds(30);
});

// Użycie przez DI
public class OrderHandler
{
    private readonly IUserService _userService;

    public OrderHandler(IUserService userService)
    {
        _userService = userService; // To jest proxy HTTP
    }

    public async Task HandleOrder(int userId)
    {
        var result = await _userService.GetUserAsync(userId);
        // → GET https://api.example.com/user-service/get-user?id=123

        if (result.IsSuccess)
        {
            var user = result.Value;
            // ...
        }
        else if (result.IsNotFound)
        {
            // ...
        }
    }
}
```

### 5. Opcjonalne atrybuty (dla customizacji)

Dla przypadków wymagających niestandardowego mapowania:

```csharp
// Voyager.Common.Proxy.Abstractions (opcjonalna zależność)
[ServiceRoute("api/v2/users")]  // Custom base path
public interface IUserService
{
    [HttpGet("by-email/{email}")]  // Custom endpoint
    Task<Result<User>> GetUserByEmailAsync(string email);

    Task<Result<User>> GetUserAsync(int id);  // Domyślna konwencja
}
```

Atrybuty są **opcjonalne** - domyślnie działa konwencja. Interfejs może mieć mieszankę: niektóre metody z atrybutami, inne z konwencją.

## Dlaczego ta opcja

### Korzyści techniczne

1. **Czysty kontrakt**
   - Interfejs nie ma żadnych zależności od frameworków HTTP
   - Może być współdzielony między serwerem a klientem bez problemów
   - Łatwe testowanie - mockujesz interfejs, nie HTTP

2. **Pełna kontrola nad `Result<T>`**
   - Naturalne mapowanie stanów Result na kody HTTP
   - Brak exceptions dla błędów biznesowych (404, 400)
   - Type-safety zachowane przez cały stack

3. **Convention over configuration**
   - Zero konfiguracji dla standardowych przypadków
   - Nazwa metody = intencja = HTTP verb
   - Atrybuty tylko gdy potrzebne

4. **Spójność ekosystemu Voyager**
   - Integracja z `Voyager.Common.Results`
   - Ten sam styl API co inne biblioteki Voyager
   - Dokumentacja w jednym miejscu

### Korzyści procesowe

1. **Redukcja boilerplate**
   - Brak ręcznych kontrolerów ASP.NET
   - Brak ręcznych klientów HTTP
   - Jedna zmiana w interfejsie = automatyczna zmiana wszędzie

2. **Trudniej o błędy**
   - Compile-time sprawdzanie zgodności klient-serwer
   - Brak desynchronizacji URL-i
   - Brak zapomnianych aktualizacji klienta

3. **Łatwiejsze onboarding**
   - Nowy developer definiuje interfejs
   - Serwer i klient "po prostu działają"
   - Mniej do nauki niż pełny ASP.NET + HttpClient

## Alternatywy które odrzuciliśmy

### Alternatywa 1: Refit z wrapper pattern

```csharp
// Czysty interfejs
public interface IUserService { ... }

// Osobny interfejs Refit (duplikacja!)
public interface IUserServiceRefit
{
    [Get("/user")]
    Task<Result<User>> GetUserAsync(int id);
}

// Wrapper łączący oba
public class UserServiceClient : IUserService
{
    private readonly IUserServiceRefit _refit;
    // ... delegowanie każdej metody
}
```

**Dlaczego odrzucona:**
- Duplikacja każdego interfejsu
- Wrapper do utrzymania przy każdej zmianie
- Więcej kodu niż własne rozwiązanie

### Alternatywa 2: Source Generator dla Refit

Generowanie interfejsu Refit z czystego interfejsu w compile-time.

**Dlaczego odrzucona:**
- Skomplikowana implementacja source generatora
- Debugowanie generowanego kodu trudne
- Nadal zależność od Refit w runtime

### Alternatywa 3: gRPC zamiast REST

**Dlaczego odrzucona:**
- Wymaga .proto files (kolejny język)
- Binarny protokół - trudniejsze debugowanie
- Mniejsza kompatybilność z istniejącymi systemami
- Nie pasuje do REST-owego ekosystemu Voyager

### Alternatywa 4: Pozostać przy ręcznych kontrolerach i klientach

**Dlaczego odrzucona:**
- Duży boilerplate
- Łatwo o desynchronizację klient-serwer
- Nie skaluje się przy wielu serwisach

## Implementacja

### Faza 1: Core (MVP)

- [ ] `Voyager.Common.Proxy.Client` - DispatchProxy + HttpClient
- [ ] `Voyager.Common.Proxy.Server` - Minimal API endpoint generation
- [ ] Konwencja nazewnictwa metod
- [ ] Mapowanie Result<T> ↔ HTTP
- [ ] Integracja z `IServiceCollection`

### Faza 2: Rozszerzenia

- [ ] Opcjonalne atrybuty dla customizacji
- [ ] Retry policies (Polly integration)
- [ ] Circuit breaker
- [ ] Request/Response logging
- [ ] OpenAPI/Swagger generation

### Faza 3: Tooling

- [ ] Diagnostyka w runtime (health checks)
- [ ] Metryki (requests/s, latency)
- [ ] Compile-time validation (analyzer)

## Struktura projektów

```
Voyager.Common.Proxy/
├── src/
│   ├── Voyager.Common.Proxy.Abstractions/   # Opcjonalne atrybuty
│   │   ├── ServiceRouteAttribute.cs
│   │   ├── HttpGetAttribute.cs
│   │   └── ...
│   ├── Voyager.Common.Proxy.Client/         # Klient HTTP
│   │   ├── ServiceProxy.cs                  # DispatchProxy implementation
│   │   ├── ServiceProxyOptions.cs
│   │   ├── HttpResultMapper.cs              # HTTP → Result<T>
│   │   └── ServiceCollectionExtensions.cs   # AddServiceProxy<T>
│   └── Voyager.Common.Proxy.Server/         # Serwer Minimal API
│       ├── EndpointGenerator.cs             # Interface → endpoints
│       ├── ResultHttpMapper.cs              # Result<T> → HTTP
│       └── EndpointRouteBuilderExtensions.cs # MapServiceEndpoints<T>
├── tests/
│   ├── Voyager.Common.Proxy.Client.Tests/
│   └── Voyager.Common.Proxy.Server.Tests/
├── docs/
│   └── adr/
│       └── ADR-001-ServiceProxy-Architecture.md (ten dokument)
└── samples/
    ├── SampleContracts/
    ├── SampleServer/
    └── SampleClient/
```

## Zależności

```
Voyager.Common.Proxy.Abstractions
└── (brak zależności)

Voyager.Common.Proxy.Client
├── Voyager.Common.Results
├── Voyager.Common.Proxy.Abstractions (opcjonalne)
├── Microsoft.Extensions.Http
└── System.Text.Json

Voyager.Common.Proxy.Server
├── Voyager.Common.Results
├── Voyager.Common.Proxy.Abstractions (opcjonalne)
├── Microsoft.AspNetCore.App (framework reference)
└── System.Text.Json
```

## Ryzyka i mitigacje

| Ryzyko | Prawdopodobieństwo | Impact | Mitigacja |
|--------|-------------------|--------|-----------|
| Reflection performance | Średnie | Niski | Cache metadata; rozważ source generator w przyszłości |
| Nieobsługiwane edge cases | Wysokie | Średni | Atrybuty dla customizacji; dobre testy |
| Breaking changes w Result<T> | Niskie | Wysoki | Wersjonowanie zgodne z Voyager.Common.Results |
| Trudność debugowania | Średnie | Średni | Szczegółowe logi; diagnostyczne middleware |

## Kiedy sprawdzimy czy to działa

**Milestone 1 (MVP):**
- [ ] Podstawowa komunikacja klient-serwer działa
- [ ] Wszystkie typy Result<T> poprawnie mapowane
- [ ] Integracja z DI działa
- [ ] Testy jednostkowe i integracyjne

**Milestone 2 (Production ready):**
- [ ] Użyte w co najmniej jednym projekcie produkcyjnym
- [ ] Performance benchmarks akceptowalne
- [ ] Dokumentacja kompletna
- [ ] Brak krytycznych bugów przez 2 tygodnie

**Metryki sukcesu:**
- Redukcja boilerplate o >70% w porównaniu do ręcznych kontrolerów
- Zero desynchronizacji klient-serwer (compile-time safety)
- Czas wdrożenia nowego endpointu < 5 minut

---

**Powiązane dokumenty:**
- [Voyager.Common.Results](https://github.com/Voyager-Poland/Voyager.Common.Results)
- [Microsoft Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
- [DispatchProxy Class](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy)
