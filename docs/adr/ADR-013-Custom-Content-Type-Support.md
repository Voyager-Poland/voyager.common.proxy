# ADR-013: Obsługa niestandardowych Content-Type w odpowiedziach proxy

**Status:** Zaimplementowane (Opcja A)
**Data:** 2026-02-10

## Problem

Podczas migracji `SaleController` w projekcie Voyager.Pilot.Backend na wzorzec proxy, dwa endpointy nie mogły zostać przeniesione do `ISaleService`, ponieważ zwracają odpowiedzi `text/html` zamiast `application/json`:

```csharp
[AllowAnonymous]
public async Task<HttpResponseMessage> ConfirmCart(uint id)
{
    return new HttpResponseMessage()
    {
        Content = new StringContent("OK", Encoding.UTF8, "text/html")
    };
}

[AllowAnonymous]
public async Task<HttpResponseMessage> PayConfirmSubOrder(uint idOrder, int idSession)
{
    var result = await _epayService.PayConfirmSubOrder(idOrder, idSession);

    if (result.ErrCode == 0)
        return new HttpResponseMessage()
        {
            Content = new StringContent("OK", Encoding.UTF8, "text/html")
        };

    return new HttpResponseMessage()
    {
        Content = new StringContent(result.ErrMessage, Encoding.UTF8, "text/html"),
        StatusCode = HttpStatusCode.Continue
    };
}
```

Te endpointy są wywoływane przez zewnętrzny system płatności (ePay), który oczekuje odpowiedzi `text/html` z treścią `OK` (bez cudzysłowów JSON). Obecny `ResponseWriter` w proxy zawsze serializuje odpowiedzi jako `application/json`, co powoduje:

- `Result<string>.Success("OK")` → `Content-Type: application/json`, Body: `"OK"` (z cudzysłowami)
- Oczekiwane → `Content-Type: text/html`, Body: `OK` (bez cudzysłowów)

W efekcie te endpointy muszą pozostać w tradycyjnym kontrolerze Web API, co wymusza utrzymywanie dwóch mechanizmów routingu dla tego samego prefixu URL (`api/Sale`).

## Wymaganie

Proxy powinno umożliwiać zwracanie odpowiedzi z niestandardowym Content-Type (np. `text/html`, `text/plain`) dla wybranych metod interfejsu, bez opakowywania treści w JSON.

## Propozycje rozwiązania

### Opcja A: Atrybut `[ProducesContentType]` na metodzie interfejsu

```csharp
[ServiceRoute("api/Sale")]
public interface ISaleService
{
    [HttpGet("ConfirmCart")]
    [AllowAnonymous]
    [ProducesContentType("text/html")]
    Task<Result<string>> ConfirmCartAsync(uint id);
}
```

Gdy metoda ma `[ProducesContentType("text/html")]`, `ResponseWriter` wysyła `Value` jako surowy string z odpowiednim Content-Type, zamiast serializować go jako JSON.

### Opcja B: Specjalny typ zwracany `RawResponse`

```csharp
public class RawResponse
{
    public string Content { get; set; }
    public string ContentType { get; set; }
    public int StatusCode { get; set; } = 200;
}

// Użycie:
[HttpGet("ConfirmCart")]
[AllowAnonymous]
Task<Result<RawResponse>> ConfirmCartAsync(uint id);
```

`ResponseWriter` rozpoznaje `RawResponse` i wysyła treść z podanym Content-Type.

### Opcja C: Interfejs `IProxyRawResult`

```csharp
public interface IProxyRawResult
{
    string Content { get; }
    string ContentType { get; }
    int? StatusCode { get; }
}
```

Każdy typ implementujący `IProxyRawResult` jest traktowany przez `ResponseWriter` jako surowa odpowiedź.

## Rekomendacja

**Opcja A** - najprostsza w użyciu, deklaratywna, nie wymaga nowych typów. Ograniczona do `string` jako treści odpowiedzi, co jest wystarczające dla typowych przypadków (callback płatności, redirecty, strony HTML).

## Implementacja

### Nowe elementy

- `ProducesContentTypeAttribute` (`Voyager.Common.Proxy.Abstractions`) — atrybut `[ProducesContentType("text/html")]` na metodzie interfejsu
- `IResponseWriter.WriteRawAsync(string content, string contentType, int statusCode)` — nowa metoda w interfejsie response writera
- `EndpointDescriptor.ContentType` — opcjonalna właściwość przekazująca custom content type

### Zmodyfikowane elementy

- `ServiceScanner` — odczytuje `ProducesContentTypeAttribute` i przekazuje do `EndpointDescriptor`
- `RequestDispatcher` — w success path, gdy `ContentType != null && value is string`, używa `WriteRawAsync` zamiast `WriteJsonAsync`
- `AspNetCoreResponseWriter` — implementacja `WriteRawAsync`
- `OwinResponseWriter` — implementacja `WriteRawAsync`
- `ServiceProxySwaggerGenerator` — 200 response używa custom content-type i inline string schema

### Zakres

- **Serwer**: tak (ASP.NET Core + OWIN)
- **Klient**: nie (callbacki są wywoływane przez zewnętrzny system, nie przez proxy klienta)
- **Swagger**: tak (custom content-type w 200 response, error responses zawsze `application/json`)
- **Error path**: bez zmian (zawsze `application/json`)
