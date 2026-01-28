# Szczegółowy przewodnik po zasadzie "Tell Don't Ask"

Zasada "Tell Don't Ask" to jeden z fundamentalnych wzorców programowania obiektowego, który promuje enkapsulację i spójność obiektów. Ten przewodnik zawiera szczegółowe przykłady i wskazówki, które pomogą Ci (i GitHub Copilotowi) zrozumieć tę zasadę w praktyce.

## Podstawowe założenia

1. **Obiekty powinny mieć zachowania, a nie tylko dane**
2. **Polecaj obiektom, co mają zrobić, zamiast pytać o ich stan**
3. **Metody powinny operować na własnych danych, a nie na danych przekazanych z zewnątrz**

## Typowe antywzorce i ich rozwiązania

### Antywzorzec 1: Podejmowanie decyzji na podstawie typu obiektu

```csharp
// ŹLE
if (payment is CreditCardPayment)
{
    ProcessCreditCard((CreditCardPayment)payment);
}
else if (payment is BankTransferPayment)
{
    ProcessBankTransfer((BankTransferPayment)payment);
}
```

```csharp
// DOBRZE
payment.Process(); // Każdy typ płatności wie, jak się przetworzyć
```

### Antywzorzec 2: Zmiana stanu obiektu z zewnątrz

```csharp
// ŹLE
if (order.Status == OrderStatus.New)
{
    order.Status = OrderStatus.Processing;
    // Dodatkowa logika
    notificationService.SendProcessingNotification(order.CustomerId);
    inventory.ReserveItems(order.Items);
}
```

```csharp
// DOBRZE
order.StartProcessing(notificationService, inventory);

// W klasie Order:
public void StartProcessing(INotificationService notificationService, IInventory inventory)
{
    if (Status != OrderStatus.New)
        throw new InvalidOperationException("Cannot start processing an order that is not new");
        
    Status = OrderStatus.Processing;
    notificationService.SendProcessingNotification(CustomerId);
    inventory.ReserveItems(Items);
}
```

### Antywzorzec 3: Gettery do wewnętrznych kolekcji

```csharp
// ŹLE
var cart = customer.GetShoppingCart();
cart.Add(product); // Zmiana stanu obiektu cart bez wiedzy customer
```

```csharp
// DOBRZE
customer.AddToCart(product); // Customer zarządza swoim koszykiem

// W klasie Customer:
public void AddToCart(Product product)
{
    _shoppingCart.Add(product);
    // Dodatkowa logika związana z dodaniem produktu
}
```

### Antywzorzec 4: Logika warunkowa oparta na stanie

```csharp
// ŹLE
if (user.IsAdmin)
{
    document.Delete();
}
else if (document.OwnerId == user.Id)
{
    document.Delete();
}
else
{
    throw new UnauthorizedException();
}
```

```csharp
// DOBRZE
user.DeleteDocument(document); // User zna swoje uprawnienia

// W klasie User:
public void DeleteDocument(Document document)
{
    if (IsAdmin || document.OwnerId == Id)
    {
        document.Delete();
    }
    else
    {
        throw new UnauthorizedException();
    }
}

// JESZCZE LEPIEJ - z wykorzystaniem oddzielnych serwisów autoryzacji
documentService.Delete(document, user);

// W klasie DocumentService:
public void Delete(Document document, User user)
{
    _authorizationService.EnsureCanDelete(document, user);
    document.Delete();
}
```

### Antywzorzec 5: Pytanie o konfigurację środowiska

```csharp
// ŹLE
if (environment.IsDevelopment())
{
    logger.WriteToConsole(message);
}
else
{
    logger.WriteToFile(message);
}
```

```csharp
// DOBRZE
logger.Log(message); // Logger sam wie, jak logować w danym środowisku

// Z wykorzystaniem fabryki:
public static ILogger CreateLogger(IEnvironment environment)
{
    if (environment.IsDevelopment())
        return new ConsoleLogger();
    else
        return new FileLogger();
}
```

## Techniki implementacji zasady "Tell Don't Ask"

### 1. Enkapsulacja logiki biznesowej

Upewnij się, że logika związana z daną encją znajduje się w tej encji:

```csharp
// DOBRZE
public class Order
{
    public void Cancel(ICancellationPolicy policy)
    {
        if (!policy.CanCancel(this))
            throw new InvalidOperationException("Order cannot be cancelled");
            
        Status = OrderStatus.Cancelled;
        CancellationDate = DateTime.UtcNow;
    }
}
```

### 2. Zastosowanie Wzorca Strategii

Zamiast sprawdzać typ, wykorzystaj polimorfizm:

```csharp
// Interfejs dla strategii płatności
public interface IPaymentStrategy
{
    PaymentResult Process(decimal amount);
}

// Implementacje dla różnych metod płatności
public class CreditCardStrategy : IPaymentStrategy { ... }
public class PayPalStrategy : IPaymentStrategy { ... }

// Użycie
public void ProcessPayment(decimal amount, IPaymentStrategy paymentStrategy)
{
    var result = paymentStrategy.Process(amount);
    // Obsługa wyniku
}
```

### 3. Użycie interfejsów zamiast implementacji

Zależności od abstrakcji zamiast konkretów:

```csharp
// Interfejs z zachowaniem
public interface IDiscountCalculator
{
    decimal CalculateDiscount(Order order);
}

// Użycie
public class OrderService
{
    private readonly IDiscountCalculator _discountCalculator;
    
    public OrderService(IDiscountCalculator discountCalculator)
    {
        _discountCalculator = discountCalculator;
    }
    
    public decimal CalculateFinalPrice(Order order)
    {
        var discount = _discountCalculator.CalculateDiscount(order);
        return order.SubTotal - discount;
    }
}
```

### 4. Metody rozszerzające dla zachowań zależnych od zewnętrznych systemów

```csharp
// Metoda rozszerzająca dla operacji na obiekcie, które wymagają zewnętrznych zależności
public static class OrderExtensions
{
    public static void Ship(this Order order, IShippingService shippingService)
    {
        if (order.Status != OrderStatus.Ready)
            throw new InvalidOperationException("Order is not ready to ship");
            
        var trackingNumber = shippingService.CreateShipment(order.ShippingAddress, order.Items);
        order.Status = OrderStatus.Shipped;
        order.TrackingNumber = trackingNumber;
    }
}

// Użycie
order.Ship(shippingService);
```

## Kiedy można naruszyć zasadę?

Jak każda zasada, "Tell Don't Ask" ma swoje wyjątki. Oto sytuacje, kiedy możesz rozważyć jej naruszenie:

1. **Obiekty DTO (Data Transfer Objects)** - ich jedynym celem jest przenoszenie danych, nie powinny zawierać logiki
2. **Warstwa prezentacji** - pobieranie danych do wyświetlenia jest naturalnym przypadkiem zastosowania getterów
3. **Mapowanie obiektów** - podczas konwersji między różnymi reprezentacjami danych
4. **Obiekty wartościowe (Value Objects)** - szczególnie te immutable, które są głównie nośnikami danych

## Jak ocenić, czy kod narusza zasadę "Tell Don't Ask"?

Pytania, które pomogą zidentyfikować naruszenia:

1. Czy kod pobiera dane z obiektu, a następnie podejmuje decyzje na ich podstawie zamiast delegować tę odpowiedzialność do obiektu?
2. Czy kod modyfikuje stan wewnętrzny obiektu z zewnątrz, zamiast prosić obiekt o wykonanie operacji?
3. Czy logika związana z obiektem znajduje się poza tym obiektem?
4. Czy kod zawiera serie getterów z jednego obiektu, aby wykonać operację?

Jeśli odpowiedź na którekolwiek z tych pytań brzmi "tak", prawdopodobnie naruszasz zasadę "Tell Don't Ask".

## Narzędzia wspierające przestrzeganie zasady

1. **Analiza statyczna kodu** - narzędzia takie jak SonarQube mogą wykrywać niektóre antywzorce
2. **Code reviews** - używaj listy kontrolnej z powyższymi pytaniami
3. **Testy jednostkowe** - pisz testy, które sprawdzają zachowanie, a nie stan wewnętrzny
4. **Metryki kodu** - monitoruj liczbę publicznych getterów i setterów w klasach

## Przykład transformacji kodu

### Przed refaktoryzacją:

```csharp
public class OrderProcessor
{
    public void Process(Order order)
    {
        // Pytanie o stan i podejmowanie decyzji
        if (order.GetStatus() == OrderStatus.New)
        {
            // Modyfikacja stanu z zewnątrz
            order.SetStatus(OrderStatus.Processing);
            
            // Pobieranie kolekcji i operowanie na niej
            var items = order.GetItems();
            foreach (var item in items)
            {
                var product = item.GetProduct();
                var inventory = product.GetInventory();
                
                // Modyfikacja stanu innego obiektu
                inventory.SetQuantity(inventory.GetQuantity() - item.GetQuantity());
            }
            
            // Pytanie o stan i podejmowanie decyzji
            if (order.GetCustomer().GetPreferredNotificationType() == NotificationType.Email)
            {
                emailService.SendOrderProcessingEmail(order.GetCustomer().GetEmail());
            }
            else
            {
                smsService.SendOrderProcessingSms(order.GetCustomer().GetPhone());
            }
        }
    }
}
```

### Po refaktoryzacji:

```csharp
public class OrderProcessor
{
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    
    public OrderProcessor(IEmailService emailService, ISmsService smsService)
    {
        _emailService = emailService;
        _smsService = smsService;
    }
    
    public void Process(Order order)
    {
        // Delegujemy odpowiedzialność do obiektu Order
        order.StartProcessing();
        
        // Delegujemy odpowiedzialność do obiektu Customer
        order.Customer.NotifyOrderProcessing(_emailService, _smsService);
    }
}

public class Order
{
    public OrderStatus Status { get; private set; }
    public Customer Customer { get; }
    private readonly List<OrderItem> _items = new();
    
    public void StartProcessing()
    {
        if (Status != OrderStatus.New)
            throw new InvalidOperationException("Can only process new orders");
            
        Status = OrderStatus.Processing;
        
        // Order zarządza swoimi elementami
        foreach (var item in _items)
        {
            item.ReserveInInventory();
        }
    }
}

public class OrderItem
{
    public Product Product { get; }
    public int Quantity { get; }
    
    public void ReserveInInventory()
    {
        // OrderItem wie, jak zarezerwować produkt
        Product.ReserveStock(Quantity);
    }
}

public class Customer
{
    public NotificationType PreferredNotificationType { get; }
    public string Email { get; }
    public string Phone { get; }
    
    public void NotifyOrderProcessing(IEmailService emailService, ISmsService smsService)
    {
        // Customer wie, jaki typ powiadomienia preferuje
        if (PreferredNotificationType == NotificationType.Email)
        {
            emailService.SendOrderProcessingEmail(Email);
        }
        else
        {
            smsService.SendOrderProcessingSms(Phone);
        }
    }
}
```

## Podsumowanie

Zasada "Tell Don't Ask" prowadzi do kodu, który jest bardziej modularny, elastyczny i łatwiejszy w utrzymaniu. Obiekty stają się odpowiedzialne za swoje zachowanie, co zwiększa spójność i enkapsulację. Choć istnieją sytuacje, kiedy naruszenie tej zasady jest uzasadnione, powinna ona być jednym z głównych wzorców myślowych podczas projektowania obiektowego.

Pamiętaj: **Mów obiektom, co mają zrobić, zamiast pytać je o dane i podejmować decyzje za nie.**