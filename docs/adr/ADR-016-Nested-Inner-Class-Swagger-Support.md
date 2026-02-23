# ADR-016: Obsługa klas zagnieżdżonych (nested inner classes) w Swagger

**Status:** Proponowany
**Data:** 2026-02-23

## Problem

Typy odpowiedzi zawierające **klasy wewnętrzne** (nested inner classes) są niepoprawnie reprezentowane w dokumentacji Swagger — zagnieżdżone obiekty pojawiają się jako `"string"` zamiast pełnej struktury.

### Przykład

Typ odpowiedzi:

```csharp
public class InvoiceData
{
    public bool IsInvoice { get; set; }
    public BuyerData Buyer { get; set; } = new();

    public class BuyerData
    {
        public string Nip { get; set; } = "";
        public string Name { get; set; } = "";
        public string Street { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string City { get; set; } = "";
    }
}
```

Aktualny wynik w Swagger UI (Example Value):

```json
{
  "isInvoice": true,
  "buyer": "string"
}
```

Oczekiwany wynik:

```json
{
  "isInvoice": true,
  "buyer": {
    "nip": "string",
    "name": "string",
    "street": "string",
    "postalCode": "string",
    "city": "string"
  }
}
```

### Kontekst architektury

Generacja Swagger opiera się na trzech warstwach (ADR-005, Opcja E):

```
SchemaGenerator (Swagger.Core, netstandard2.0)
    ↓ generuje schematy z reflection
ServiceProxyDocumentFilter (Swagger / Swagger.Owin)
    ↓ konwertuje na OpenAPI format
    ↓ ⚠️ DELEGUJE do Swashbuckle/Swagger.Net dla typów złożonych
Swashbuckle / Swagger.Net
    ↓ renderuje Swagger UI
```

### Przyczyna 1: Delegacja schema generation do bibliotek zewnętrznych

Oba adaptery odrzucają schematy z proxy's `SchemaGenerator` i delegują generację do biblioteki Swagger:

**ASP.NET Core** — `ServiceProxyDocumentFilter.ConvertToOpenApiSchema()`:

```csharp
// Swagger/ServiceProxyDocumentFilter.cs, linie 152-157
if (schema.ClrType != null && !IsPrimitiveType(schema.ClrType) && !IsCollectionType(schema.ClrType))
{
    return schemaGenerator.GenerateSchema(schema.ClrType, schemaRepository); // ← Swashbuckle
}
```

**OWIN** — `ServiceProxyDocumentFilter.ConvertToSwaggerSchema()`:

```csharp
// Swagger.Owin/ServiceProxyDocumentFilter.cs, linie 216-226
if (schema.ClrType != null && !IsPrimitiveType(schema.ClrType) && !IsCollectionType(schema.ClrType))
{
    try
    {
        return schemaRegistry.GetOrRegister(schema.ClrType); // ← Swagger.Net
    }
    catch
    {
        // Fallback — cichy błąd
    }
}
```

Proxy's `SchemaGenerator` **poprawnie** obsługuje klasy wewnętrzne — `typeof(InvoiceData.BuyerData).GetProperties(Public | Instance)` zwraca wszystkie 5 właściwości. Ale wynik tego generatora jest **odrzucany** na rzecz Swashbuckle/Swagger.Net, które mogą nie poradzić sobie z typem `InvoiceData+BuyerData` (CLR nazwa nested class).

Przepływ danych:

```
SchemaGenerator:
  InvoiceData → {isInvoice: boolean, buyer: $ref BuyerData}  ✅ poprawne
  BuyerData   → {nip: string, name: string, ...}             ✅ poprawne
      ↓
ConvertToOpenApiSchema:
  schema.ClrType = typeof(InvoiceData) → nie-primitive, nie-collection
  → DELEGUJ do Swashbuckle                                   ⚠️ proxy's schema odrzucony
      ↓
Swashbuckle/Swagger.Net:
  typeof(InvoiceData+BuyerData) → ???                         ❌ "string"
```

### Przyczyna 2: Kolizja nazw schematów dla klas zagnieżdżonych

`SchemaGenerator.GetSchemaName()` używa `type.Name`:

```csharp
// Swagger.Core/SchemaGenerator.cs, linie 284-289
private static string GetSchemaName(Type type)
{
    if (!type.IsGenericType)
    {
        return type.Name;  // ← BuyerData (bez uwzględnienia DeclaringType)
    }
    // ...
}
```

Dla klas wewnętrznych `type.Name` zwraca **tylko** nazwę klasy zagnieżdżonej:

| Typ CLR | `type.Name` | `type.FullName` |
|---------|-------------|-----------------|
| `InvoiceData.BuyerData` | `"BuyerData"` | `"...InvoiceData+BuyerData"` |
| `OrderData.BuyerData` | `"BuyerData"` | `"...OrderData+BuyerData"` |

Jeśli dwa serwisy proxy definiują klasy o tej samej nazwie wewnętrznej w różnych typach nadrzędnych, schemat drugiego nadpisze pierwszy w `_componentSchemas["BuyerData"]`.

### Brak pokrycia testowego

Istniejące testy (`SchemaGeneratorTests.cs`) testują typ `NestedClass` z właściwością `SimpleClass`:

```csharp
public class NestedClass
{
    public int Id { get; set; }
    public SimpleClass? Child { get; set; }  // ← SimpleClass to klasa SIOSTRZANA, nie wewnętrzna
}
```

Nazwa `NestedClass` jest myląca — `SimpleClass` **nie jest** klasą wewnętrzną `NestedClass`. Brak testów dla prawdziwego wzorca C# inner class (`class Outer { class Inner { } }`).

## Decyzja

### 1. Usunięcie delegacji do Swashbuckle/Swagger.Net

Proxy's `SchemaGenerator` staje się **jedynym źródłem prawdy** dla schematów typów. Usunąć bloki delegacji w obu adapterach:

**ASP.NET Core** — usunąć linie 152-157 z `ConvertToOpenApiSchema()`.

**OWIN** — usunąć linie 216-226 z `ConvertToSwaggerSchema()`.

Po usunięciu, `ConvertToOpenApiSchema` zawsze przetwarza schemat proxy's `SchemaGenerator`:
- Referencje (`$ref`) → `OpenApiReference`
- Obiekty z Properties → rekurencyjne mapowanie
- Prymitywy → bezpośrednie mapowanie type/format

To eliminuje zależność od tego, jak Swashbuckle/Swagger.Net radzi sobie z konkretnymi typami CLR.

### 2. Naprawa `GetSchemaName` dla klas zagnieżdżonych

Uwzględnić `DeclaringType` w nazwie schematu:

```csharp
private static string GetSchemaName(Type type)
{
    // Nested/inner classes: InvoiceData.BuyerData → "InvoiceDataBuyerData"
    if (type.DeclaringType != null)
    {
        return GetSchemaName(type.DeclaringType) + type.Name;
    }

    if (!type.IsGenericType)
    {
        return type.Name;
    }

    // ... existing generic handling
}
```

Wynik:

| Typ CLR | Stara nazwa | Nowa nazwa |
|---------|-------------|------------|
| `InvoiceData.BuyerData` | `"BuyerData"` | `"InvoiceDataBuyerData"` |
| `OrderData.BuyerData` | `"BuyerData"` | `"OrderDataBuyerData"` |
| `SimpleClass` (top-level) | `"SimpleClass"` | `"SimpleClass"` (bez zmian) |

### 3. Testy dla klas wewnętrznych

Nowe scenariusze testowe w `SchemaGeneratorTests.cs`:

```csharp
public class OuterClass
{
    public bool Flag { get; set; }
    public InnerData Data { get; set; }
    public class InnerData
    {
        public string Code { get; set; } = "";
        public int Value { get; set; }
    }
}

public class AnotherOuter
{
    public InnerData Info { get; set; }
    public class InnerData
    {
        public string Label { get; set; } = "";
    }
}
```

| Test | Asercja |
|------|---------|
| Inner class generuje pełny schemat z properties | `Properties` zawiera `code` (string) i `value` (integer) |
| Schema name uwzględnia DeclaringType | `ComponentSchemas` zawiera klucz `"OuterClassInnerData"` |
| Dwie klasy InnerData nie kolidują | `ComponentSchemas` zawiera `"OuterClassInnerData"` i `"AnotherOuterInnerData"` |
| ComponentSchemas zawiera outer + inner | Oba klucze obecne: `"OuterClass"` i `"OuterClassInnerData"` |
| Właściwość inner class jest referencją | `Properties["data"].IsReference == true` |

## Alternatywy

### 1. Konfiguracja Swashbuckle's `CustomSchemaIds`

```csharp
services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(type =>
        type.DeclaringType != null
            ? $"{type.DeclaringType.Name}{type.Name}"
            : type.Name);
});
```

**Odrzucone**, ponieważ:
- Nie rozwiązuje problemu w OWIN (Swagger.Net nie ma odpowiednika `CustomSchemaIds`)
- Leaky abstraction — konfiguracja Swashbuckle „wyciekałaby" do konsumenta proxy
- Nie eliminuje fundamentalnego problemu delegacji do bibliotek zewnętrznych

### 2. Flattening — zakaz klas wewnętrznych w kontraktach

Wymaganie przeniesienia `BuyerData` na poziom top-level:

```csharp
// Zamiast InvoiceData.BuyerData → osobna klasa BuyerData
public class BuyerData { ... }
public class InvoiceData
{
    public BuyerData Buyer { get; set; }
}
```

**Odrzucone**, ponieważ:
- Wymusza zmianę modeli domenowych w projektach konsumenckich
- Klasy wewnętrzne to poprawny wzorzec C# — proxy nie powinno ograniczać języka
- Breaking change dla istniejących kontraktów

### 3. Walidacja + wyjątek dla klas wewnętrznych (analogicznie do ADR-015)

**Odrzucone**, ponieważ:
- Klasy wewnętrzne **mogą** być poprawnie obsługiwane — wystarczy naprawić generator
- Zakaz jest nieproporcjonalny — problem jest w implementacji, nie w samym wzorcu

## Konsekwencje

- **Pozytywne**: Klasy wewnętrzne w typach odpowiedzi renderowane poprawnie w Swagger UI
- **Pozytywne**: Brak kolizji nazw schematów dla klas o tej samej nazwie w różnych typach nadrzędnych
- **Pozytywne**: Proxy's `SchemaGenerator` jako jedyne źródło prawdy — przewidywalne zachowanie niezależne od wersji Swashbuckle/Swagger.Net
- **Pozytywne**: Identyczne zachowanie na obu platformach (ASP.NET Core + OWIN)
- **Negatywne**: Zmiana nazw schematów w swagger.json (`"BuyerData"` → `"InvoiceDataBuyerData"`) — breaking change dla klientów generowanych z OpenAPI spec
- **Negatywne**: Utrata ewentualnych usprawnień Swashbuckle (np. nullable annotations) — proxy's `SchemaGenerator` musi sam obsługiwać edge cases

## Pliki do modyfikacji

| Plik | Zmiana |
|------|--------|
| `Swagger.Core/SchemaGenerator.cs` | Fix `GetSchemaName` — `DeclaringType` |
| `Swagger/ServiceProxyDocumentFilter.cs` | Usunięcie delegacji do Swashbuckle |
| `Swagger.Owin/ServiceProxyDocumentFilter.cs` | Usunięcie delegacji do Swagger.Net |
| `Tests/SchemaGeneratorTests.cs` | Nowe testy: inner classes, kolizje nazw |

## Powiązane dokumenty

- [ADR-005: Swagger/OpenAPI Integration](./ADR-005-Swagger-OpenAPI-Integration.md) — architektura Swagger.Core + adaptery
- [ADR-015: Array/Collection Query Parameter Support](./ADR-015-Array-Collection-Query-Parameter-Support.md) — analogiczny problem z cichymi błędami
