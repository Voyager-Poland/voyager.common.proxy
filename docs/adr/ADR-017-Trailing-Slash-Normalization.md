# ADR-017: Normalizacja trailing slash w routingu

**Status:** Zaimplementowane
**Data:** 2026-02-26

## Problem

Klient wysyła żądanie `POST /api/Vip/Contact/` (z trailing slash), ale serwer proxy zwraca **404 Not Found**. Żądanie `POST /api/Vip/Contact` (bez trailing slash) działa poprawnie.

### Reprodukcja

Kontrakt:

```csharp
[RequireAuthorization]
[ServiceRoute("api/Vip")]
public interface IVipProxyService
{
    [HttpPost("Contact")]
    Task<Result<VipClientContactUpsertResponse>> UpsertVipContactAsync(VipClientContactUpsertRequest request);
}
```

Wynikowy route template: `/api/Vip/Contact`

| Żądanie | Wynik |
|---------|-------|
| `POST /api/Vip/Contact` | 200 OK |
| `POST /api/Vip/Contact/` | **404 Not Found** |

### Kontekst

Poprzedni kontroler ASP.NET Core (`[ApiController]` + `[Route]`) akceptował oba warianty, ponieważ domyślny routing ASP.NET Core normalizuje trailing slash. Po migracji na proxy framework to zachowanie zostało utracone — jest to **regresja**.

Klient (Angular frontend) wysyła URL z trailing slash od dawna. Zmiana klienta jest niepraktyczna — wiele endpointów, wiele zespołów.

### Analiza techniczna

**ASP.NET Core** (`MapMethods`) — domyślny routing ASP.NET Core **obsługuje** oba warianty. Jeśli serwis korzysta z hostingu ASP.NET Core i problem mimo to występuje, przyczyną może być middleware przechwytujący request przed dotarciem do endpoint routing (np. custom path matching).

**OWIN** (`RouteMatcher`) — regex w [RouteMatcher.cs](../../src/Voyager.Common.Proxy.Server.Owin/RouteMatcher.cs), linia 69-72:

```csharp
pattern = "^" + Regex.Replace(
    routeTemplate,
    @"\{(\w+)(?::[^}]+)?\}",
    _ => "([^/]+)") + "$";
```

Dla route template `/api/Vip/Contact` generowany pattern to:

```
^/api/Vip/Contact$
```

Anchor `$` wymaga **exact match** — `/api/Vip/Contact/` nie pasuje, bo zawiera dodatkowy `/` na końcu.

## Decyzja

Normalizacja trailing slash na obu platformach — serwer proxy akceptuje żądania zarówno z trailing slash, jak i bez niego, zachowując kompatybilność z domyślnym zachowaniem ASP.NET Core.

### 1. OWIN: Fix w `RouteMatcher.BuildRegexPattern`

Zmiana anchora regex z `$` na `/?$`:

```csharp
// Przed:
pattern = "^" + Regex.Replace(
    routeTemplate,
    @"\{(\w+)(?::[^}]+)?\}",
    _ => "([^/]+)") + "$";

// Po:
pattern = "^" + Regex.Replace(
    routeTemplate,
    @"\{(\w+)(?::[^}]+)?\}",
    _ => "([^/]+)") + "/?$";
```

Wyrażenie `/?` oznacza opcjonalny `/` — pattern `/api/Vip/Contact/?$` pasuje zarówno do `/api/Vip/Contact`, jak i do `/api/Vip/Contact/`.

### 2. ASP.NET Core: Weryfikacja i opcjonalny middleware

Standardowy `MapMethods` powinien obsługiwać oba warianty. Jeśli mimo to problem występuje (np. przez middleware przechwytujący request), dodać middleware normalizujący ścieżkę:

```csharp
// W konfiguracji pipeline, przed UseRouting():
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path?.Length > 1 && path.EndsWith("/"))
    {
        context.Request.Path = new PathString(path.TrimEnd('/'));
    }
    await next();
});
```

Warunek `path?.Length > 1` zabezpiecza root path `/` przed trimowaniem do pustego stringa.

**Uwaga:** Middleware to rozwiązanie opcjonalne — do zastosowania tylko jeśli weryfikacja potwierdzi, że problem występuje także na ASP.NET Core.

### 3. Testy

Nowe scenariusze testowe:

**Unit testy — `RouteMatcherTests.cs`** (OWIN, net48):

| Test | Asercja |
|------|---------|
| Static route bez trailing slash | `TryMatch("/api/Vip/Contact", ...)` → `true` |
| Static route z trailing slash | `TryMatch("/api/Vip/Contact/", ...)` → `true` |
| Static route z dodatkowym znakiem (suffix) | `TryMatch("/api/Vip/Contactx", ...)` → `false` |
| Static route z podwójnym trailing slash | `TryMatch("/api/Vip/Contact//", ...)` → `false` |
| Static route z dodatkowym segmentem | `TryMatch("/api/Vip/Contact/extra", ...)` → `false` |
| Parameterized route bez trailing slash | `TryMatch("/api/users/123", ...)` → `true`, `routeValues["id"] == "123"` |
| Parameterized route z trailing slash | `TryMatch("/api/users/123/", ...)` → `true`, `routeValues["id"] == "123"` |
| Parameterized route z dodatkowym znakiem (suffix) | `TryMatch("/api/users/123/ordersx", ...)` → `false` |
| Root route bez trailing slash | `TryMatch("/status", ...)` → `true` |
| Root route z trailing slash | `TryMatch("/status/", ...)` → `true` |
| Case insensitive z trailing slash | `TryMatch("/API/VIP/CONTACT/", ...)` → `true` |

**Integration testy — `TrailingSlashIntegrationTests.cs`** (ASP.NET Core, net6.0/net8.0):

| Test | Asercja |
|------|---------|
| GET convention route bez trailing slash | 200 OK |
| GET convention route z trailing slash | 200 OK |
| POST convention route z trailing slash | 200 OK |
| GET ServiceRoute bez trailing slash | 200 OK |
| GET ServiceRoute z trailing slash | 200 OK |
| GET parameterized route bez trailing slash | 200 OK |
| GET parameterized route z trailing slash | 200 OK |
| POST NoPrefix route bez trailing slash | 200 OK |
| POST NoPrefix route z trailing slash | 200 OK |

## Alternatywy

### 1. Wymuszenie braku trailing slash po stronie klienta

Wymaganie od zespołów frontendowych usunięcia trailing slash ze wszystkich URL-ów.

**Odrzucone**, ponieważ:
- Regresja po stronie serwera nie powinna być naprawiana po stronie klienta
- Wiele zespołów, wiele endpointów — koordynacja kosztowna
- Poprzedni kontroler akceptował oba warianty — klient działał poprawnie
- RFC 7230 nie zabrania trailing slash — serwer powinien być tolerancyjny

### 2. Rejestracja podwójnych route templates (z `/` i bez)

Dla każdego endpointu rejestrować dwa warianty:

```csharp
endpoints.MapMethods("/api/Vip/Contact", ...);
endpoints.MapMethods("/api/Vip/Contact/", ...);
```

**Odrzucone**, ponieważ:
- Podwaja liczbę zarejestrowanych endpointów
- Komplikuje Swagger — duplikaty w dokumentacji
- Wymaga zmian w `ServiceScanner` i `ServiceProxyEndpointRouteBuilderExtensions`
- Niepotrzebna złożoność — normalizacja jest prostsza

### 3. Brak zmian — udokumentowanie jako "by design"

**Odrzucone**, ponieważ:
- Jest to regresja względem standardowego zachowania ASP.NET Core
- Łamie zasadę najmniejszego zaskoczenia (principle of least surprise)
- Koszt naprawy jest minimalny (zmiana jednego znaku w regex)

## Konsekwencje

- **Pozytywne**: Zachowanie spójne z domyślnym routingiem ASP.NET Core — brak regresji po migracji z kontrolerów na proxy
- **Pozytywne**: Klienci nie muszą być modyfikowani
- **Pozytywne**: Minimalna zmiana — jedna linia w `RouteMatcher`, opcjonalny middleware dla ASP.NET Core
- **Negatywne**: Brak — trailing slash normalization to standard w web frameworkach

## Implementacja

### Faza 1: Fix RouteMatcher ✅

- [x] Zmiana anchora regex `$` → `/?$` w `BuildRegexPattern` → [RouteMatcher.cs](../../src/Voyager.Common.Proxy.Server.Owin/RouteMatcher.cs)
- [x] `InternalsVisibleTo` dla projektu testowego → [Voyager.Common.Proxy.Server.Owin.csproj](../../src/Voyager.Common.Proxy.Server.Owin/Voyager.Common.Proxy.Server.Owin.csproj)

### Faza 2: Testy ✅

- [x] Unit testy RouteMatcher (11 scenariuszy, net48) → [RouteMatcherTests.cs](../../tests/Voyager.Common.Proxy.Server.Tests/RouteMatcherTests.cs)
- [x] Integration testy ASP.NET Core (9 scenariuszy, net6.0/net8.0) → [TrailingSlashIntegrationTests.cs](../../tests/Voyager.Common.Proxy.Server.IntegrationTests/TrailingSlashIntegrationTests.cs)
- [x] Reference Server.Owin w Server.Tests (conditional net48) → [Voyager.Common.Proxy.Server.Tests.csproj](../../tests/Voyager.Common.Proxy.Server.Tests/Voyager.Common.Proxy.Server.Tests.csproj)

## Pliki zmodyfikowane

| Plik | Zmiana |
|------|--------|
| `Server.Owin/RouteMatcher.cs` | `$` → `/?$` w `BuildRegexPattern` |
| `Server.Owin/Voyager.Common.Proxy.Server.Owin.csproj` | `InternalsVisibleTo` |
| `Server.Tests/Voyager.Common.Proxy.Server.Tests.csproj` | Conditional reference na Server.Owin (net48) |
| `Server.Tests/RouteMatcherTests.cs` | **Nowy** — 11 unit testów |
| `Server.IntegrationTests/TrailingSlashIntegrationTests.cs` | **Nowy** — 9 integration testów |

## Powiązane dokumenty

- [ADR-001: ServiceProxy Architecture](./ADR-001-ServiceProxy-Architecture.md)
- [ADR-004: Server MultiPlatform Support](./ADR-004-Server-MultiPlatform-Support.md) — OWIN + ASP.NET Core
- [ADR-012: Empty ServiceRoute Prefix](./ADR-012-Empty-ServiceRoute-Prefix.md) — powiązana zmiana w routingu
