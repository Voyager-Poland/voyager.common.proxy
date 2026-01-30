# ADR-006: Pre-Method Authorization Filter dla Service Proxy Middleware

**Status:** Zaimplementowane
**Data:** 2026-01-30
**Autor:** [Do uzupełnienia]

## Problem

Serwisy w stylu DDD (interfejsy reprezentujące domenę) często wymagają **sprawdzenia uprawnień przed wywołaniem każdej metody**. Obecnie logika ta musi być implementowana w każdej metodzie serwisu osobno, co prowadzi do:

1. **Duplikacji kodu** - każda metoda musi sprawdzać uprawnienia
2. **Łatwości pominięcia** - programista może zapomnieć dodać sprawdzanie
3. **Mieszania odpowiedzialności** - logika biznesowa miesza się z autoryzacją

### Obecny stan

```csharp
// Każda metoda musi sama sprawdzać uprawnienia
public class VIPService : IVIPService
{
    private readonly IPilotIdentity _identity;
    private readonly ActionModule _actionModule;

    public async Task<Result<PassengerList>> GetPassengersAsync(string date)
    {
        // Każda metoda musi to powtarzać!
        var action = _actionModule.GetService<GetPassengersAction>(_identity);
        return await action.ExecuteAsync(new GetPassengersRequest(date));
    }

    public async Task<Result<Booking>> CreateBookingAsync(BookingRequest request)
    {
        // To samo tutaj...
        var action = _actionModule.GetService<CreateBookingAction>(_identity);
        return await action.ExecuteAsync(request);
    }
}
```

### Pożądany stan

Middleware powinien **automatycznie** sprawdzać uprawnienia **przed** wywołaniem metody serwisu:

```
Request → Middleware → [Permission Check] → Service Method
                              ↓
                        403 Forbidden
```

### Wymagania

1. **Automatyczne sprawdzanie** - przed każdym wywołaniem metody serwisu
2. **Konfigurowalny checker** - użytkownik dostarcza logikę sprawdzania
3. **Dostęp do kontekstu** - checker musi mieć dostęp do:
   - Kontekstu żądania (user, claims)
   - Informacji o wywoływanej metodzie
   - Parametrów wywołania (opcjonalnie)
4. **Opcjonalność** - niektóre serwisy nie potrzebują tego mechanizmu
5. **Wieloplatformowość** - OWIN i ASP.NET Core

## Kontekst techniczny

### Istniejący pattern w organizacji: Action<TRequest, TResponse>

```csharp
public abstract class Action<TRequest, TResponse>
{
    private readonly IPilotIdentity pilotIdentity;

    public Task<Result<TResponse>> ExecuteAsync(TRequest request)
    {
        var result = ValidateRequest(request);
        if (!result.IsSuccess)
            return Task.FromResult(Result<TResponse>.Failure(result.Error));

        return CheckPermissionsAsync(request, pilotIdentity)
            .BindAsync(() => ProcessAsync(request));
    }

    protected virtual Task<Result> CheckPermissionsAsync(
        TRequest request,
        IPilotIdentity pilotIdentity)
    {
        return Task.FromResult(Result.Success());
    }

    protected abstract Task<Result<TResponse>> ProcessAsync(TRequest request);
}
```

**Kluczowe:** `CheckPermissionsAsync` jest wywoływane **przed** `ProcessAsync`.

### Istniejąca autoryzacja w middleware

Obecnie middleware wspiera tylko **deklaratywną** autoryzację przez atrybuty:

```csharp
[RequireAuthorization(Roles = "Admin")]
public interface IAdminService { ... }
```

To sprawdza tylko **czy user jest w roli**, ale nie pozwala na **fine-grained permission checking** oparty na:
- Danych z requestu
- Stanie aplikacji
- Złożonej logice biznesowej

## Propozycje rozwiązań

---

## Opcja A: Permission Checker Callback

### Opis

Middleware przyjmuje callback `Func<PermissionContext, Task<Result>>` który jest wywoływany przed każdym wywołaniem metody serwisu.

### Architektura

```
┌─────────────────────────────────────────────────────────────────┐
│                    Request Flow                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  1. Request arrives                                              │
│  2. Route matching → finds endpoint                             │
│  3. [RequireAuthorization] check (existing)                     │
│  4. ────────────────────────────────────────────────────────    │
│  5. NEW: PermissionChecker.CheckAsync(context)                  │
│     │                                                            │
│     ├─► Success → continue to step 6                            │
│     └─► Failure → return 403 Forbidden                          │
│  6. ────────────────────────────────────────────────────────    │
│  7. Create service instance                                      │
│  8. Dispatch to service method                                   │
│  9. Return response                                              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Model danych

```csharp
/// <summary>
/// Context provided to permission checker before method invocation.
/// </summary>
public class PermissionContext
{
    /// <summary>
    /// The authenticated user principal.
    /// </summary>
    public IPrincipal? User { get; }

    /// <summary>
    /// The service interface type being called.
    /// </summary>
    public Type ServiceType { get; }

    /// <summary>
    /// The method being invoked.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// The endpoint descriptor with route info.
    /// </summary>
    public EndpointDescriptor Endpoint { get; }

    /// <summary>
    /// The deserialized request parameters (if available).
    /// Key = parameter name, Value = parameter value.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    /// <summary>
    /// Platform-specific request context.
    /// OWIN: IDictionary&lt;string, object&gt;
    /// ASP.NET Core: HttpContext
    /// </summary>
    public object RawContext { get; }
}
```

### Implementacja OWIN

```csharp
public static class ServiceProxyOwinMiddleware
{
    /// <summary>
    /// Creates middleware with optional permission checking.
    /// </summary>
    public static Func<AppFunc, AppFunc> Create<TService>(
        Func<TService> serviceFactory,
        Func<PermissionContext, Task<Result>>? permissionChecker = null)
        where TService : class
    {
        // ...
    }

    /// <summary>
    /// Creates middleware with options including permission checker.
    /// </summary>
    public static Func<AppFunc, AppFunc> Create<TService>(
        Action<ServiceProxyOptions<TService>> configure)
        where TService : class
    {
        var options = new ServiceProxyOptions<TService>();
        configure(options);
        // ...
    }
}

public class ServiceProxyOptions<TService>
{
    /// <summary>
    /// Factory to create service instances.
    /// </summary>
    public Func<TService>? ServiceFactory { get; set; }

    /// <summary>
    /// Context-aware factory (receives OWIN environment).
    /// </summary>
    public Func<IDictionary<string, object>, TService>? ContextAwareFactory { get; set; }

    /// <summary>
    /// Optional permission checker called before each method invocation.
    /// Return Result.Success() to allow, Result.Failure() to deny.
    /// </summary>
    public Func<PermissionContext, Task<Result>>? PermissionChecker { get; set; }
}
```

### Przykład użycia

```csharp
// OWIN
app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
{
    options.ServiceFactory = () => container.Resolve<IVIPService>();

    options.PermissionChecker = async context =>
    {
        var identity = PilotIdentityFactory.Create(context.User);

        // Sprawdź uprawnienia bazując na metodzie i parametrach
        var checker = container.Resolve<IVIPPermissionChecker>();
        return await checker.CheckAsync(
            identity,
            context.Method.Name,
            context.Parameters);
    };
}));

// Serwis bez permission checkera
app.Use(ServiceProxyOwinMiddleware.Create<IPublicService>(
    () => container.Resolve<IPublicService>()));
```

```csharp
// ASP.NET Core
app.MapServiceProxy<IVIPService>(options =>
{
    options.PermissionChecker = async context =>
    {
        var identity = pilotIdentityFactory.Create(context.User);
        var checker = context.HttpContext.RequestServices
            .GetRequiredService<IVIPPermissionChecker>();
        return await checker.CheckAsync(identity, context.Method.Name);
    };
});
```

### Integracja z Action pattern

```csharp
// Permission checker może używać istniejących Actions
options.PermissionChecker = async context =>
{
    var identity = PilotIdentityFactory.Create(context.User);
    var actionModule = container.Resolve<ActionModule>();

    // Znajdź odpowiedni Action dla metody
    var actionType = GetActionTypeForMethod(context.Method);
    if (actionType == null)
        return Result.Success(); // Brak Action = brak sprawdzania

    // Utwórz Action i wywołaj tylko CheckPermissionsAsync
    var action = actionModule.CreateAction(actionType, identity);
    var request = BuildRequestFromParameters(context.Parameters);
    return await action.CheckPermissionsOnlyAsync(request);
};
```

### Zalety

| Zaleta | Opis |
|--------|------|
| **Elastyczność** | Pełna kontrola nad logiką sprawdzania |
| **Dostęp do parametrów** | Checker widzi parametry wywołania |
| **Opcjonalność** | Nie trzeba używać jeśli niepotrzebne |
| **Integracja z Action** | Można wykorzystać istniejące CheckPermissionsAsync |

### Wady

| Wada | Opis |
|------|------|
| **Boilerplate** | Trzeba napisać checker dla każdego serwisu |
| **Duplikacja** | Podobna logika może się powtarzać między serwisami |
| **Deserializacja** | Parametry muszą być zdeserializowane przed checkerem |

---

## Opcja B: IServicePermissionChecker Interface

### Opis

Definiujemy interfejs `IServicePermissionChecker<TService>` który implementuje logikę sprawdzania uprawnień dla konkretnego serwisu.

### Architektura

```
┌─────────────────────────────────────────────────────────────────┐
│                    Abstractions                                  │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  public interface IServicePermissionChecker<TService>           │
│  {                                                               │
│      Task<Result> CheckPermissionAsync(                         │
│          IPrincipal? user,                                      │
│          MethodInfo method,                                      │
│          IReadOnlyDictionary<string, object?> parameters);      │
│  }                                                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    User Implementation                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  public class VIPServicePermissionChecker                       │
│      : IServicePermissionChecker<IVIPService>                   │
│  {                                                               │
│      private readonly ActionModule _actionModule;                │
│                                                                  │
│      public async Task<Result> CheckPermissionAsync(...)        │
│      {                                                           │
│          var identity = PilotIdentity.FromPrincipal(user);      │
│          var action = GetActionForMethod(method);               │
│          return await action.CheckPermissionsOnlyAsync(...);    │
│      }                                                           │
│  }                                                               │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### Implementacja

```csharp
// Voyager.Common.Proxy.Server.Abstractions
public interface IServicePermissionChecker<TService>
    where TService : class
{
    /// <summary>
    /// Checks if the user has permission to invoke the specified method.
    /// </summary>
    /// <param name="user">The authenticated user (may be null).</param>
    /// <param name="method">The method being invoked.</param>
    /// <param name="parameters">The method parameters.</param>
    /// <returns>Success if allowed, Failure with reason if denied.</returns>
    Task<Result> CheckPermissionAsync(
        IPrincipal? user,
        MethodInfo method,
        IReadOnlyDictionary<string, object?> parameters);
}
```

### Użycie

```csharp
// Rejestracja checkera w DI
container.RegisterType<
    IServicePermissionChecker<IVIPService>,
    VIPServicePermissionChecker>();

// OWIN - automatycznie znajdzie checker z DI
app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
{
    options.ServiceFactory = () => container.Resolve<IVIPService>();
    options.UsePermissionChecker = true; // szuka IServicePermissionChecker<T>
}));

// Lub explicit
app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
{
    options.ServiceFactory = () => container.Resolve<IVIPService>();
    options.PermissionChecker = container.Resolve<IServicePermissionChecker<IVIPService>>();
}));
```

### Przykład implementacji checkera

```csharp
public class VIPServicePermissionChecker : IServicePermissionChecker<IVIPService>
{
    private readonly ActionModule _actionModule;
    private readonly Dictionary<string, Type> _methodToActionMap;

    public VIPServicePermissionChecker(ActionModule actionModule)
    {
        _actionModule = actionModule;
        _methodToActionMap = new Dictionary<string, Type>
        {
            ["GetPassengersAsync"] = typeof(GetPassengersAction),
            ["CreateBookingAsync"] = typeof(CreateBookingAction),
            // ...
        };
    }

    public async Task<Result> CheckPermissionAsync(
        IPrincipal? user,
        MethodInfo method,
        IReadOnlyDictionary<string, object?> parameters)
    {
        if (!_methodToActionMap.TryGetValue(method.Name, out var actionType))
        {
            // Brak mapowania = brak sprawdzania uprawnień
            return Result.Success();
        }

        var identity = PilotIdentity.FromPrincipal(user);
        var action = _actionModule.CreateAction(actionType, identity);
        var request = BuildRequest(actionType, parameters);

        return await action.CheckPermissionsOnlyAsync(request);
    }
}
```

### Zalety

| Zaleta | Opis |
|--------|------|
| **Typed interface** | Silne typowanie per-service |
| **Testowalność** | Łatwo testować w izolacji |
| **DI friendly** | Naturalna integracja z kontenerami |
| **Reusable** | Jeden checker per serwis |

### Wady

| Wada | Opis |
|------|------|
| **Więcej kodu** | Trzeba pisać klasy checkerów |
| **Mapowanie metod** | Trzeba mapować metody na Actions |

---

## Opcja C: Attribute-Based z Custom Handler

### Opis

Rozszerzenie istniejących atrybutów o możliwość wskazania custom handlera sprawdzającego uprawnienia.

### Implementacja

```csharp
// Atrybut wskazujący handler
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
public class RequirePermissionCheckAttribute : Attribute
{
    public Type? CheckerType { get; set; }
}

// Użycie
[RequirePermissionCheck(CheckerType = typeof(VIPPermissionChecker))]
public interface IVIPService
{
    Task<Result<PassengerList>> GetPassengersAsync(string date);
}

// Checker
public class VIPPermissionChecker : IPermissionChecker
{
    public Task<Result> CheckAsync(PermissionContext context)
    {
        // ...
    }
}
```

### Zalety

| Zaleta | Opis |
|--------|------|
| **Deklaratywne** | Widoczne w interfejsie |
| **Per-method** | Można różne checkery per metoda |

### Wady

| Wada | Opis |
|------|------|
| **Mniej elastyczne** | Checker musi być znany w compile time |
| **Zależność w interfejsie** | Interfejs zależy od checkera |

---

## Opcja D: Hybrid - Callback + Optional Interface (Rekomendowana)

### Opis

Kombinacja prostego callbacku (dla szybkich przypadków) z opcjonalnym interfejsem (dla złożonych przypadków).

### API

```csharp
public class ServiceProxyOptions<TService>
{
    // Option 1: Simple factory
    public Func<TService>? ServiceFactory { get; set; }

    // Option 2: Context-aware factory
    public Func<IDictionary<string, object>, TService>? ContextAwareFactory { get; set; }

    // Permission checking - pick one:

    // Option A: Inline callback (simple cases)
    public Func<PermissionContext, Task<Result>>? PermissionChecker { get; set; }

    // Option B: Typed checker from DI (complex cases)
    public IServicePermissionChecker<TService>? PermissionCheckerInstance { get; set; }

    // Option C: Auto-resolve from DI
    public bool ResolvePermissionCheckerFromDI { get; set; }
}
```

### Przykłady użycia

**Prosty przypadek - inline callback:**

```csharp
app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
{
    options.ServiceFactory = () => container.Resolve<IVIPService>();

    options.PermissionChecker = async ctx =>
    {
        if (ctx.User?.Identity?.IsAuthenticated != true)
            return Result.Failure(Error.Unauthorized());

        // Prosta logika inline
        if (ctx.Method.Name == "DeleteAsync" && !ctx.User.IsInRole("Admin"))
            return Result.Failure(Error.Forbidden("Admin required"));

        return Result.Success();
    };
}));
```

**Złożony przypadek - dedykowany checker:**

```csharp
// Rejestracja
container.RegisterType<VIPServicePermissionChecker>();

app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
{
    options.ServiceFactory = () => container.Resolve<IVIPService>();
    options.PermissionCheckerInstance = container.Resolve<VIPServicePermissionChecker>();
}));
```

**Auto-resolve z DI:**

```csharp
// Rejestracja checkera z konwencją nazewnictwa
container.RegisterType<
    IServicePermissionChecker<IVIPService>,
    VIPServicePermissionChecker>();

app.Use(ServiceProxyOwinMiddleware.Create<IVIPService>(options =>
{
    options.ServiceFactory = () => container.Resolve<IVIPService>();
    options.ResolvePermissionCheckerFromDI = true;
}));
```

**Bez permission checkera (public service):**

```csharp
app.Use(ServiceProxyOwinMiddleware.Create<IPublicService>(
    () => container.Resolve<IPublicService>()));
```

### Implementacja middleware

```csharp
public async Task Invoke(IDictionary<string, object> environment)
{
    var (endpoint, routeValues) = _matcher.Match(method, path);
    if (endpoint == null)
    {
        await _next(environment);
        return;
    }

    // 1. Existing authorization check ([RequireAuthorization])
    var authInfo = GetAuthorizationInfo(endpoint);
    var authResult = AuthorizationChecker.CheckAuthorization(environment, authInfo);
    if (!authResult.Succeeded)
    {
        await WriteAuthorizationFailureResponse(environment, authResult);
        return;
    }

    // 2. NEW: Permission checker (if configured)
    if (_permissionChecker != null)
    {
        var permContext = await BuildPermissionContext(environment, endpoint, routeValues);
        var permResult = await _permissionChecker(permContext);

        if (!permResult.IsSuccess)
        {
            await WritePermissionFailureResponse(environment, permResult);
            return;
        }
    }

    // 3. Create service and dispatch
    var service = CreateService(environment);
    var requestContext = new OwinRequestContext(environment, routeValues);
    var responseWriter = new OwinResponseWriter(environment);

    await _dispatcher.DispatchAsync(requestContext, responseWriter, endpoint, service);
}
```

### Zalety

| Zaleta | Opis |
|--------|------|
| **Elastyczność** | Callback dla prostych, interface dla złożonych |
| **Opcjonalność** | Można w ogóle nie używać |
| **Wieloplatformowość** | Ten sam pattern OWIN i Core |
| **Backwards compatible** | Istniejący kod bez zmian |
| **Integracja z Action** | Checker może delegować do Action.CheckPermissionsAsync |

### Wady

| Wada | Opis |
|------|------|
| **Wiele opcji** | API może być confusing |

---

## Porównanie opcji

| Kryterium | Opcja A | Opcja B | Opcja C | **Opcja D** |
|-----------|---------|---------|---------|-------------|
| | Callback | Interface | Attribute | **Hybrid** |
| **Elastyczność** | Wysoka | Średnia | Niska | **Wysoka** |
| **Testowalność** | Średnia | Wysoka | Średnia | **Wysoka** |
| **Prostota użycia** | Wysoka | Średnia | Wysoka | **Wysoka** |
| **DI friendly** | Średnio | Tak | Tak | **Tak** |
| **Typed** | Nie | Tak | Tak | **Opcjonalnie** |
| **Inline logic** | Tak | Nie | Nie | **Tak** |

## Rekomendacja

### Rekomendowana: Opcja D (Hybrid)

**Uzasadnienie:**

1. **Elastyczność** - prosty callback lub dedykowany checker
2. **Opcjonalność** - nie wymusza wzorca na wszystkich serwisach
3. **Integracja z Action pattern** - checker może używać istniejących Actions
4. **Wieloplatformowa** - działa dla OWIN i ASP.NET Core

## Decyzja

**Wybrana opcja:** Opcja D (Hybrid - Callback + Optional Interface)

**Zakres implementacji:**

### Faza 1: Core abstrakcje

- [ ] `PermissionContext` class w `Server.Abstractions`
- [ ] `IServicePermissionChecker<TService>` interface
- [ ] `ServiceProxyOptions<TService>` class

### Faza 2: OWIN

- [ ] Nowy overload `Create<TService>(Action<ServiceProxyOptions<TService>>)`
- [ ] Modyfikacja `ServiceProxyMiddleware` do wywołania permission checkera
- [ ] Testy jednostkowe

### Faza 3: ASP.NET Core

- [ ] Nowy overload `MapServiceProxy<TService>(Action<ServiceProxyOptions<TService>>)`
- [ ] Integracja z existing middleware
- [ ] Testy jednostkowe

### Faza 4: Dokumentacja

- [ ] README z przykładami
- [ ] Przykłady integracji z Action pattern
- [ ] Przykłady dla różnych scenariuszy

---

**Powiązane dokumenty:**
- [ADR-004: Server Multi-Platform Support](./ADR-004-Server-MultiPlatform-Support.md)
- [ADR-005: Swagger/OpenAPI Integration](./ADR-005-Swagger-OpenAPI-Integration.md)
