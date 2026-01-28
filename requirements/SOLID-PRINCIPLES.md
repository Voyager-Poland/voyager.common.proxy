# SOLID Principles - Voyager Team Standards

This document explains how to apply SOLID principles in our codebase.

## Quick Reference

| Principle | Summary | Key Question |
|-----------|---------|--------------|
| **S**ingle Responsibility | One class, one reason to change | Does this class do too many things? |
| **O**pen/Closed | Open for extension, closed for modification | Can I add features without changing existing code? |
| **L**iskov Substitution | Derived classes must be substitutable | Can I swap child for parent? |
| **I**nterface Segregation | Many specific interfaces > one general | Is this interface too fat? |
| **D**ependency Inversion | Depend on abstractions, not concretions | Am I depending on interfaces or concrete classes? |

---

## Single Responsibility Principle (SRP)

> A class should have only one reason to change.

### ✅ GOOD Example

```csharp
/// <summary>
/// Validates user data.
/// </summary>
public class UserValidator
{
    public Result<User> Validate(User user)
    {
        if (string.IsNullOrEmpty(user.Email))
            return Error.ValidationError("Email is required");
        
        if (!user.Email.Contains("@"))
            return Error.ValidationError("Invalid email format");
        
        return user;
    }
}

/// <summary>
/// Manages user persistence.
/// </summary>
public class UserRepository
{
    private readonly DbContext _context;
    
    public Result<User> Save(User user)
    {
        try
        {
            _context.Users.Add(user);
            _context.SaveChanges();
            return user;
        }
        catch (DbException ex)
        {
            return Error.DatabaseError("Failed to save user", ex);
        }
    }
}

/// <summary>
/// Sends user-related emails.
/// </summary>
public class UserEmailService
{
    private readonly IEmailSender _emailSender;
    
    public Result SendWelcomeEmail(User user)
    {
        var email = new Email
        {
            To = user.Email,
            Subject = "Welcome!",
            Body = $"Hello {user.Name}, welcome to our platform!"
        };
        
        return _emailSender.Send(email);
    }
}

/// <summary>
/// Coordinates user registration workflow.
/// </summary>
public class UserRegistrationService
{
    private readonly UserValidator _validator;
    private readonly UserRepository _repository;
    private readonly UserEmailService _emailService;
    
    public UserRegistrationService(
        UserValidator validator,
        UserRepository repository,
        UserEmailService emailService)
    {
        _validator = validator;
        _repository = repository;
        _emailService = emailService;
    }
    
    public Result<User> Register(User user)
    {
        return _validator.Validate(user)
            .Bind(u => _repository.Save(u))
            .Tap(u => _emailService.SendWelcomeEmail(u));
    }
}
```

### ❌ BAD Example

```csharp
/// <summary>
/// Does EVERYTHING related to users - TOO MANY RESPONSIBILITIES!
/// </summary>
public class UserService
{
    public Result<User> Register(User user)
    {
        // Responsibility 1: Validation
        if (string.IsNullOrEmpty(user.Email))
            return Error.ValidationError("Email required");
        
        // Responsibility 2: Database
        _context.Users.Add(user);
        _context.SaveChanges();
        
        // Responsibility 3: Email
        _emailSender.Send(new Email { /*...*/ });
        
        // Responsibility 4: Logging
        _logger.Log($"User {user.Id} registered");
        
        // Responsibility 5: Cache
        _cache.Set($"user-{user.Id}", user);
        
        // ❌ This class has 5 reasons to change!
        return user;
    }
}
```

---

## Open/Closed Principle (OCP)

> Software entities should be open for extension but closed for modification.

### ✅ GOOD Example - Using Interfaces

```csharp
/// <summary>
/// Interface for payment processing.
/// </summary>
public interface IPaymentProcessor
{
    Result<Payment> Process(decimal amount, string currency);
}

/// <summary>
/// Processes credit card payments.
/// </summary>
public class CreditCardProcessor : IPaymentProcessor
{
    public Result<Payment> Process(decimal amount, string currency)
    {
        // Credit card logic
        return new Payment { Amount = amount };
    }
}

/// <summary>
/// Processes PayPal payments.
/// </summary>
public class PayPalProcessor : IPaymentProcessor
{
    public Result<Payment> Process(decimal amount, string currency)
    {
        // PayPal logic
        return new Payment { Amount = amount };
    }
}

/// <summary>
/// Processes Bitcoin payments - NEW! No existing code modified.
/// </summary>
public class BitcoinProcessor : IPaymentProcessor
{
    public Result<Payment> Process(decimal amount, string currency)
    {
        // Bitcoin logic
        return new Payment { Amount = amount };
    }
}

/// <summary>
/// Coordinates payment processing.
/// </summary>
public class PaymentService
{
    private readonly IPaymentProcessor _processor;
    
    // Can accept ANY payment processor - open for extension!
    public PaymentService(IPaymentProcessor processor)
    {
        _processor = processor;
    }
    
    public Result<Payment> ProcessPayment(decimal amount)
    {
        return _processor.Process(amount, "USD");
    }
}
```

### ❌ BAD Example - Modification Required

```csharp
public class PaymentService
{
    public Result<Payment> ProcessPayment(decimal amount, string paymentType)
    {
        // ❌ Every new payment type requires modifying this method
        switch (paymentType)
        {
            case "CreditCard":
                // Credit card logic
                break;
            case "PayPal":
                // PayPal logic
                break;
            case "Bitcoin": // ❌ Had to modify existing code!
                // Bitcoin logic
                break;
            default:
                return Error.ValidationError("Unknown payment type");
        }
        
        return new Payment { Amount = amount };
    }
}
```

---

## Liskov Substitution Principle (LSP)

> Derived classes must be substitutable for their base classes.

### ✅ GOOD Example

```csharp
/// <summary>
/// Base repository for entity operations.
/// </summary>
public abstract class Repository<T>
{
    protected readonly DbContext Context;
    
    protected Repository(DbContext context)
    {
        Context = context;
    }
    
    public virtual Result<T> GetById(int id)
    {
        try
        {
            var entity = Context.Set<T>().Find(id);
            
            if (entity is null)
                return Error.NotFoundError($"Entity {id} not found");
            
            return Result<T>.Success(entity);
        }
        catch (Exception ex)
        {
            return Error.DatabaseError("Database error", ex);
        }
    }
}

/// <summary>
/// User-specific repository.
/// </summary>
public class UserRepository : Repository<User>
{
    public UserRepository(DbContext context) : base(context) { }
    
    // ✅ Returns the same Result<User> as base class
    public override Result<User> GetById(int id)
    {
        // Can add user-specific logic
        var result = base.GetById(id);
        
        if (result.IsSuccess)
        {
            // Additional user-specific processing
        }
        
        return result; // ✅ Still returns Result<User>
    }
}

/// <summary>
/// Order-specific repository.
/// </summary>
public class OrderRepository : Repository<Order>
{
    public OrderRepository(DbContext context) : base(context) { }
    
    // ✅ Can be used anywhere Repository<Order> is expected
}

// Usage - substitution works perfectly
public class ReportService
{
    public Result<Report> GenerateReport(Repository<Order> repository)
    {
        var orders = repository.GetById(123); // Works with any derived class
        // ...
    }
}

var service = new ReportService();
service.GenerateReport(new OrderRepository(context)); // ✅ Substitution works
```

### ❌ BAD Example

```csharp
public abstract class Repository<T>
{
    public abstract Result<T> GetById(int id);
}

public class UserRepository : Repository<User>
{
    // ❌ Throws exception instead of returning Result!
    public override Result<User> GetById(int id)
    {
        if (id <= 0)
            throw new ArgumentException(); // ❌ Base expects Result, not exception
        
        // ...
    }
}

public class OrderRepository : Repository<Order>
{
    // ❌ Returns null instead of Result.NotFoundError!
    public override Result<Order> GetById(int id)
    {
        var order = FindOrder(id);
        return order; // ❌ If order is null, returns Result.Success(null)!
    }
}

// ❌ Substitution broken - can't reliably swap implementations
```

---

## Interface Segregation Principle (ISP)

> Clients should not be forced to depend on interfaces they don't use.

### ✅ GOOD Example - Small, Focused Interfaces

```csharp
/// <summary>
/// Read operations only.
/// </summary>
public interface IReadRepository<T>
{
    Result<T> GetById(int id);
    Result<List<T>> GetAll();
}

/// <summary>
/// Write operations only.
/// </summary>
public interface IWriteRepository<T>
{
    Result<T> Save(T entity);
    Result Delete(int id);
}

/// <summary>
/// Query operations only.
/// </summary>
public interface IQueryableRepository<T>
{
    Result<List<T>> Query(Expression<Func<T, bool>> predicate);
}

// Clients can depend only on what they need
public class ReportService
{
    private readonly IReadRepository<Order> _repository; // ✅ Only needs read
    
    public ReportService(IReadRepository<Order> repository)
    {
        _repository = repository;
    }
}

public class OrderCreationService
{
    private readonly IWriteRepository<Order> _repository; // ✅ Only needs write
    
    public OrderCreationService(IWriteRepository<Order> repository)
    {
        _repository = repository;
    }
}

// Full implementation can implement multiple interfaces
public class OrderRepository : 
    IReadRepository<Order>, 
    IWriteRepository<Order>, 
    IQueryableRepository<Order>
{
    public Result<Order> GetById(int id) { /* */ }
    public Result<List<Order>> GetAll() { /* */ }
    public Result<Order> Save(Order entity) { /* */ }
    public Result Delete(int id) { /* */ }
    public Result<List<Order>> Query(Expression<Func<Order, bool>> predicate) { /* */ }
}
```

### ❌ BAD Example - Fat Interface

```csharp
/// <summary>
/// ❌ TOO MANY METHODS - forces clients to depend on things they don't use
/// </summary>
public interface IRepository<T>
{
    Result<T> GetById(int id);
    Result<List<T>> GetAll();
    Result<T> Save(T entity);
    Result Update(T entity);
    Result Delete(int id);
    Result<T> Find(Expression<Func<T, bool>> predicate);
    Result<List<T>> Query(string sql);
    Result BulkSave(List<T> entities);
    Result BulkDelete(List<int> ids);
    Result<int> Count();
    Result<bool> Exists(int id);
    // ❌ And 20 more methods...
}

// ❌ Client only needs GetById but must depend on entire interface
public class ReportService
{
    private readonly IRepository<Order> _repository;
    
    public ReportService(IRepository<Order> repository)
    {
        _repository = repository; // ❌ Depends on 30 methods, uses 1
    }
}
```

---

## Dependency Inversion Principle (DIP)

> High-level modules should not depend on low-level modules. Both should depend on abstractions.

### ✅ GOOD Example - Depend on Abstractions

```csharp
/// <summary>
/// Abstraction for user storage.
/// </summary>
public interface IUserRepository
{
    Result<User> GetById(int id);
    Result<User> Save(User user);
}

/// <summary>
/// Abstraction for email sending.
/// </summary>
public interface IEmailService
{
    Result SendEmail(string to, string subject, string body);
}

/// <summary>
/// High-level business logic - depends ONLY on abstractions.
/// </summary>
public class UserRegistrationService
{
    private readonly IUserRepository _repository;
    private readonly IEmailService _emailService;
    
    // ✅ Depends on interfaces, not concrete classes
    public UserRegistrationService(
        IUserRepository repository,
        IEmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }
    
    public Result<User> Register(User user)
    {
        return _repository.Save(user)
            .Tap(u => _emailService.SendEmail(
                u.Email, 
                "Welcome!", 
                $"Hello {u.Name}"));
    }
}

// ✅ Concrete implementation - can be swapped easily
public class SqlUserRepository : IUserRepository
{
    public Result<User> GetById(int id) { /* SQL logic */ }
    public Result<User> Save(User user) { /* SQL logic */ }
}

// ✅ Alternative implementation - easy to swap
public class MongoUserRepository : IUserRepository
{
    public Result<User> GetById(int id) { /* MongoDB logic */ }
    public Result<User> Save(User user) { /* MongoDB logic */ }
}

// ✅ Easy to test with fakes
public class FakeUserRepository : IUserRepository
{
    private readonly List<User> _users = new();
    
    public Result<User> GetById(int id) { /* In-memory logic */ }
    public Result<User> Save(User user) { _users.Add(user); return user; }
}
```

### ❌ BAD Example - Depend on Concretions

```csharp
/// <summary>
/// ❌ Depends directly on concrete classes - HARD TO TEST & CHANGE
/// </summary>
public class UserRegistrationService
{
    private readonly SqlUserRepository _repository; // ❌ Concrete class
    private readonly SmtpEmailService _emailService; // ❌ Concrete class
    
    public UserRegistrationService()
    {
        // ❌ Direct instantiation - tightly coupled
        _repository = new SqlUserRepository();
        _emailService = new SmtpEmailService();
    }
    
    public Result<User> Register(User user)
    {
        // ❌ Can't swap to MongoDB without changing this code
        // ❌ Can't test without real database
        // ❌ Can't test without real SMTP server
        return _repository.Save(user)
            .Tap(u => _emailService.SendEmail(u.Email, "Welcome!", "..."));
    }
}
```

---

## Dependency Injection Setup

### Startup Configuration (ASP.NET Core)

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Register interfaces with implementations
        services.AddScoped<IUserRepository, SqlUserRepository>();
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<UserValidator>();
        services.AddScoped<UserRegistrationService>();
    }
}
```

### Constructor Injection

```csharp
public class UserController : ControllerBase
{
    private readonly UserRegistrationService _registrationService;
    
    // ✅ Dependencies injected by framework
    public UserController(UserRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }
    
    [HttpPost]
    public IActionResult Register([FromBody] RegisterDto dto)
    {
        var result = _registrationService.Register(new User { /*...*/ });
        
        return result.Match<IActionResult>(
            onSuccess: user => Ok(user),
            onFailure: error => BadRequest(error.Message)
        );
    }
}
```

---

## SOLID Checklist

Before submitting code, verify:

**Single Responsibility:**
- [ ] Each class has one clear purpose
- [ ] Class name describes its single responsibility
- [ ] Only one reason for class to change

**Open/Closed:**
- [ ] New features added without modifying existing code
- [ ] Using interfaces for extensibility
- [ ] Strategy pattern used where appropriate

**Liskov Substitution:**
- [ ] Derived classes return same Result types as base
- [ ] No unexpected exceptions from overrides
- [ ] Child classes work wherever parent is expected

**Interface Segregation:**
- [ ] Interfaces are small and focused
- [ ] Clients depend only on methods they use
- [ ] No "god interfaces"

**Dependency Inversion:**
- [ ] Depending on interfaces, not concrete classes
- [ ] Using constructor injection
- [ ] No `new` keyword for dependencies

---

## Common Violations

### ❌ Violation 1: God Class
```csharp
public class UserManager
{
    // ❌ Does validation, database, email, logging, caching, auth...
}
```

### ❌ Violation 2: Switch on Type
```csharp
public Result Process(PaymentType type)
{
    switch (type) // ❌ Violates OCP
    {
        case PaymentType.CreditCard: /* */
        case PaymentType.PayPal: /* */
    }
}
```

### ❌ Violation 3: Base Class with Unused Methods
```csharp
public abstract class Repository
{
    public abstract void Save(); // ❌ Not all repos need this
}
```

### ❌ Violation 4: Tight Coupling
```csharp
public class Service
{
    private SqlDatabase _db = new SqlDatabase(); // ❌ Direct instantiation
}
```

---

## Further Reading

- [SOLID Principles by Uncle Bob](https://blog.cleancoder.com/uncle-bob/2020/10/18/Solid-Relevance.html)
- [C# SOLID Principles Examples](https://www.pluralsight.com/courses/csharp-solid-principles)
- Team training materials in Confluence

## Questions?

Ask in team chat or tag a senior developer in your PR.
