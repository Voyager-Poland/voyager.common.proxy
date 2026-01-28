# Reguły nazewnictwa kodu

## Ogólne zasady

1. **Używaj opisowych i jednoznacznych nazw**
   - Nazwy powinny jasno wyrażać cel/funkcję/znaczenie elementu
   - Preferuj pełne słowa zamiast skrótów i akronimów (chyba że są powszechnie znane)
   - Unikaj nazw jednoliterowych (z wyjątkiem zmiennych iteracyjnych w krótkich pętlach)

2. **Zachowaj spójność w całym projekcie**
   - Stosuj tę samą konwencję nazewnictwa w całym kodzie
   - Przestrzegaj standardów specyficznych dla danego języka programowania
   - Utrzymuj spójność z używanymi frameworkami i bibliotekami

3. **Czytelność ponad zwięzłość**
   - Preferuj dłuższe, ale jasne nazwy zamiast krótkich, ale niejasnych
   - Unikaj nadmiernie długich nazw, które utrudniają czytanie kodu

## Konwencje dla różnych elementów kodu

### Klasy i typy
- **PascalCase** (każde słowo zaczyna się wielką literą, bez separatorów)
- Używaj rzeczowników lub fraz rzeczownikowych
- Dodawaj sufiksy wskazujące na wzorce projektowe (np. `UserRepository`, `PaymentService`, `ProductFactory`)

```csharp
public class CustomerManager { }
public class OrderProcessingService { }
```

### Metody i funkcje
- **PascalCase** (w językach takich jak C#, Java) lub **camelCase** (JavaScript, TypeScript)
- Używaj czasowników lub fraz czasownikowych opisujących działanie
- Nazwa powinna wskazywać, co metoda robi, a nie jak to robi

```csharp
public void ProcessPayment(string orderId) { }
public bool ValidateUserCredentials(string username, string password) { }
```

### Zmienne i parametry
- **camelCase** (pierwsze słowo małą literą, każde następne wielką)
- Używaj precyzyjnych rzeczowników lub fraz rzeczownikowych
- Unikaj zbyt ogólnych nazw (`data`, `info`, `value`)

```csharp
int productCount;
string userEmail;
bool isAuthenticated;
```

### Stałe i pola readonly
- **PascalCase** (C#) lub **UPPER_SNAKE_CASE** (w wielu językach)
- Używaj rzeczowników lub fraz rzeczownikowych
- Nazwy powinny wskazywać na wartość, którą przechowują

```csharp
const int MaxRetryCount = 5;
private readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromSeconds(30);
```

### Interfejsy
- **PascalCase** z prefiksem "I" (w językach takich jak C#)
- Używaj rzeczowników, przymiotników lub fraz opisujących możliwości

```csharp
public interface IRepository { }
public interface IDisposable { }
public interface IComparable { }
```

### Przestrzenie nazw i pakiety
- **PascalCase** (każde słowo zaczyna się wielką literą)
- Używaj nazw hierarchicznych, od ogólnych do szczegółowych
- Uwzględniaj nazwę firmy/projektu i obszar funkcjonalny

```csharp
namespace CompanyName.ProjectName.Module.Submodule
```

### Pliki i foldery projektu
- Nazwa pliku powinna odpowiadać głównej klasie/interfejsowi w nim zawartym
- Zachowaj spójną strukturę katalogów według funkcjonalności lub warstw architektury
- Stosuj tę samą konwencję (PascalCase lub kebab-case) dla wszystkich plików/folderów

## Nazewnictwo Branch'y i Pull Requestów

- Branch'e związane z zadaniami w JIRA muszą zaczynać się od identyfikatora zadania
- Używaj kebab-case po identyfikatorze zadania
- Format: `[ID-ZADANIA]-krótki-opis-zmiany`

```
PROJ-123-implementacja-logowania
PROJ-456-naprawa-błędu-walidacji
```

## Nazwy testów

- Używaj wzorca: `[TestowanaMETODA]_[WARUNEK]_[OCZEKIWANYREZULTAT]`
- Stosuj pełne i opisowe nazwy wskazujące na testowany scenariusz

```csharp
public void ProcessPayment_WithValidPaymentInfo_ReturnsSuccessStatus()
public void ValidateEmail_WithInvalidFormat_ThrowsValidationException()
```

## Unikanie anty-wzorców

1. **Unikaj mylących skrótów**
   - `mgr` może oznaczać "manager", "merger", "migrator" itp.

2. **Unikaj podobnych nazw dla różnych elementów**
   - `getUserList()` i `getListOfUsers()` są mylące - wybierz jedną konwencję

3. **Nie używaj nazw sprzecznych z funkcjonalnością**
   - `validateUser()`, która nie tylko waliduje, ale też zapisuje do bazy danych

4. **Nie używaj terminów systemowych lub języka jako nazw własnych**
   - Unikaj nazywania zmiennych tak jak słowa kluczowe (np. `class`, `int`, `object`)

## Nazewnictwo w programowaniu wielowątkowym

1. **Dodawaj odpowiednie sufiksy i prefiksy wskazujące na współbieżność**
   - Używaj sufiksów `Async` dla metod asynchronicznych
   - Dodawaj `Task` do nazw metod zwracających `Task` lub `Task<T>`
   - Używaj prefiksu `Concurrent` dla kolekcji bezpiecznych wątkowo

```csharp
public async Task<Result> ProcessPaymentAsync()
public Task<User> GetUserByIdTaskAsync(int userId)
ConcurrentDictionary<string, User> activeUsers;
```

2. **Nazywaj klasy synchronizujące według ich przeznaczenia**
   - Używaj nazwisk wskazujących na rodzaj synchronizacji: `Lock`, `Semaphore`, `Mutex`
   - Dodawaj sufiksy wskazujące na zasób, który chronią

```csharp
private readonly object _userCacheLock = new object();
private SemaphoreSlim _databaseConnectionSemaphore;
```

3. **Jawnie oznaczaj zmienne współdzielone między wątkami**
   - Używaj prefiksu `shared` lub `volatile` dla zmiennych odczytywanych/zapisywanych przez wiele wątków
   - Stosuj sufiksy `ThreadSafe` do klas bezpiecznych wątkowo

```csharp
private volatile bool _isProcessing;
private long _sharedCounter;
public class UserRepositoryThreadSafe { }
```

## Zasady wspierające współpracę z AI

1. **Używaj standardowej terminologii**
   - Nazwy powszechnie używane w domenie są łatwiejsze do analizy przez AI

2. **Dodawaj prefiksy do nazw dla specjalnych przypadków**
   - np. `raw` dla nieprzetworzonych danych, `normalized` dla danych po normalizacji

3. **Umieszczaj wskazówki dla AI w komentarzach**
   ```csharp
   // AI-HINT: Ta klasa jest częścią wzorca CQRS
   public class CreateOrderCommand { }
   ```