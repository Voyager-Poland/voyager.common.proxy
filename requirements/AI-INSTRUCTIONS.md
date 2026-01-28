# AI Coding Instructions for Sindbad IT

## CRITICAL RULES - NEVER VIOLATE

### 1. Error Handling - Result Pattern ONLY

**✅ ALWAYS DO:**
```csharp
// Use Result<T> for operations that return values
public Result<User> GetUser(int userId)
{
    if (userId <= 0)
        return Error.ValidationError("User ID must be positive");

    var user = _repository.Find(userId);

    if (user is null)
        return Error.NotFoundError($"User {userId} not found");

    return user; // Implicit conversion
}

// Use Result for void operations
public Result DeleteUser(int userId)
{
    if (userId <= 0)
        return Error.ValidationError("User ID must be positive");

    _repository.Delete(userId);
    return Result.Success();
}
```

**❌ NEVER DO:**
```csharp
// DON'T throw exceptions for business logic
public User GetUser(int userId)
{
    var user = _repository.Find(userId);
    if (user is null)
        throw new NotFoundException(); // ❌ FORBIDDEN
    return user;
}

// DON'T return null
public User GetUser(int userId)
{
    return _repository.Find(userId); // ❌ Can return null - FORBIDDEN
}
```

**Exception: Only use try-catch for unexpected technical errors:**
```csharp
public Result<User> GetUser(int userId)
{
    if (userId <= 0)
        return Error.ValidationError("Invalid ID");

    try
    {
        var user = _repository.Find(userId);

        if (user is null)
            return Error.NotFoundError("User not found");

        return user;
    }
    catch (DbException ex)
    {
        // Convert unexpected exception to Result
        return Error.DatabaseError("Database error", ex);
    }
}
```

### 2. Null Handling - NEVER Return Null

**✅ ALWAYS DO:**
```csharp
// Return Result with NotFoundError instead of null
public Result<User> FindUser(string email)
{
    var user = _repository.FindByEmail(email);

    if (user is null)
        return Error.NotFoundError($"User with email {email} not found");

    return user;
}

// For collections, return empty list, never null
public Result<List<Order>> GetUserOrders(int userId)
{
    var orders = _repository.GetOrders(userId) ?? new List<Order>();
    return orders;
}

// Use nullable reference types for optional parameters
public Result<User> UpdateUser(int userId, string? newEmail = null)
{
    // newEmail can be null - it's optional
}
```

**❌ NEVER DO:**
```csharp
// DON'T return null
public User FindUser(string email)
{
    return _repository.FindByEmail(email); // ❌ Can be null - FORBIDDEN
}

// DON'T return null for collections
public List<Order> GetUserOrders(int userId)
{
    return null; // ❌ FORBIDDEN - return empty list
}
```

### 3. XML Documentation - MANDATORY for Public API

**✅ ALWAYS DO:**
```csharp
/// <summary>
/// Retrieves a user by their unique identifier.
/// </summary>
/// <param name="userId">The unique identifier of the user.</param>
/// <returns>
/// A <see cref="Result{User}"/> containing the user if found,
/// or an error if the user doesn't exist or the ID is invalid.
/// </returns>
/// <example>
/// <code>
/// var result = GetUser(123);
/// result.Match(
///     onSuccess: user => Console.WriteLine(user.Name),
///     onFailure: error => Console.WriteLine(error.Message)
/// );
/// </code>
/// </example>
public Result<User> GetUser(int userId)
{
    // Implementation
}

/// <summary>
/// Represents a user in the system.
/// </summary>
public class User
{
    /// <summary>
    /// Gets or sets the unique identifier for the user.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the user's email address.
    /// </summary>
    public string Email { get; set; }
}
```

**Minimum required tags:**
- `<summary>` - ALWAYS required
- `<param>` - For each parameter
- `<returns>` - For return value
- `<exception>` - If method can throw (rare in our codebase)
- `<example>` - Recommended for complex public APIs

### 4. Testing - No Duplicate Tests

**✅ ALWAYS DO:**
```csharp
[TestFixture]
public class UserServiceTests
{
    // Test one scenario per test
    [Test]
    public void GetUser_ValidId_ReturnsUser()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = service.GetUser(123);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.Id, Is.EqualTo(123));
    }

    [Test]
    public void GetUser_InvalidId_ReturnsValidationError()
    {
        var service = CreateService();
        var result = service.GetUser(-1);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Type, Is.EqualTo(ErrorType.Validation));
    }

    [Test]
    public void GetUser_NotFound_ReturnsNotFoundError()
    {
        var service = CreateService();
        var result = service.GetUser(999);

        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error.Type, Is.EqualTo(ErrorType.NotFound));
    }
}
```

**❌ NEVER DO:**
```csharp
// DON'T create duplicate tests for the same scenario
[Test]
public void GetUser_ValidId_Works() { } // ❌

[Test]
public void GetUser_WithValidId_ReturnsUser() { } // ❌ Duplicate of above

[Test]
public void TestGetUserWithValidId() { } // ❌ Another duplicate

// DON'T test the same thing in multiple ways unless specifically testing edge cases
```

**Test naming convention:**
```
[MethodName]_[Scenario]_[ExpectedResult]

Examples:
- GetUser_ValidId_ReturnsUser
- GetUser_InvalidId_ReturnsValidationError
- GetUser_NotFound_ReturnsNotFoundError
- CreateOrder_InsufficientBalance_ReturnsBusinessError
```

### 5. SOLID Principles

#### Single Responsibility
```csharp
// ✅ GOOD - One responsibility
public class UserValidator
{
    public Result<User> Validate(User user) { }
}

public class UserRepository
{
    public Result<User> Save(User user) { }
}

// ❌ BAD - Multiple responsibilities
public class UserService
{
    public Result<User> ValidateAndSave(User user)
    {
        // Validation logic
        // Database logic
        // Email logic
        // ❌ Too many responsibilities
    }
}
```

#### Open/Closed Principle
```csharp
// ✅ GOOD - Open for extension, closed for modification
public interface IPaymentProcessor
{
    Result<Payment> Process(decimal amount);
}

public class CreditCardProcessor : IPaymentProcessor { }
public class PayPalProcessor : IPaymentProcessor { }
```

#### Liskov Substitution
```csharp
// ✅ GOOD - Derived types are substitutable
public abstract class Repository<T>
{
    public abstract Result<T> GetById(int id);
}

public class UserRepository : Repository<User>
{
    public override Result<User> GetById(int id)
    {
        // Returns Result<User> as expected
    }
}
```

#### Interface Segregation
```csharp
// ✅ GOOD - Small, focused interfaces
public interface IReadRepository<T>
{
    Result<T> GetById(int id);
    Result<List<T>> GetAll();
}

public interface IWriteRepository<T>
{
    Result<T> Save(T entity);
    Result Delete(int id);
}

// ❌ BAD - Fat interface
public interface IRepository<T>
{
    Result<T> GetById(int id);
    Result<List<T>> GetAll();
    Result<T> Save(T entity);
    Result Delete(int id);
    Result<T> Update(T entity);
    Result BulkSave(List<T> entities);
    // Too many methods - not all clients need all of them
}
```

#### Dependency Inversion
```csharp
// ✅ GOOD - Depend on abstractions
public class UserService
{
    private readonly IUserRepository _repository;
    private readonly IEmailService _emailService;

    public UserService(IUserRepository repository, IEmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }
}

// ❌ BAD - Depend on concrete classes
public class UserService
{
    private readonly SqlUserRepository _repository; // ❌ Concrete class

    public UserService()
    {
        _repository = new SqlUserRepository(); // ❌ Direct instantiation
    }
}
```

### 6. Railway Oriented Programming

**Use Result chaining for clean code flow:**

```csharp
// ✅ GOOD - Railway operators
public Result<OrderConfirmation> PlaceOrder(int userId, CreateOrderDto dto)
{
    return GetUser(userId)
        .Ensure(
            user => user.IsActive,
            Error.BusinessError("User is not active")
        )
        .Bind(user => ValidateOrder(dto))
        .Bind(order => ProcessPayment(order))
        .Tap(order => _logger.LogInformation($"Order {order.Id} placed"))
        .Map(order => new OrderConfirmation { OrderId = order.Id });
}

// ❌ BAD - Nested if statements
public Result<OrderConfirmation> PlaceOrder(int userId, CreateOrderDto dto)
{
    var userResult = GetUser(userId);
    if (userResult.IsSuccess)
    {
        if (userResult.Value.IsActive)
        {
            var orderResult = ValidateOrder(dto);
            if (orderResult.IsSuccess)
            {
                // ❌ Pyramid of doom
            }
        }
    }
}
```

### 7. Async Operations

```csharp
// ✅ GOOD - Use async operators
public async Task<Result<User>> CreateUserAsync(UserDto dto)
{
    return await ValidateEmail(dto.Email)
        .BindAsync(email => CheckEmailUniqueAsync(email))
        .MapAsync(email => new User { Email = email })
        .BindAsync(user => SaveUserAsync(user))
        .TapAsync(user => SendWelcomeEmailAsync(user));
}

// ❌ BAD - Mixing sync and async incorrectly
public async Task<Result<User>> CreateUserAsync(UserDto dto)
{
    var result = ValidateEmail(dto.Email)
        .Map(email => SaveUserAsync(email)); // ❌ Wrong - returns Result<Task<Result<User>>>
}
```

## Code Review Checklist

Before submitting PR, verify:

- [ ] All methods returning data use `Result<T>` or `Result`
- [ ] No `throw` statements for business logic (only for unexpected errors in try-catch)
- [ ] No `return null` statements
- [ ] All public classes/methods have XML documentation
- [ ] No duplicate test scenarios
- [ ] SOLID principles followed
- [ ] Railway operators used for chaining
- [ ] Code compiles without warnings (warnings treated as errors)
- [ ] All tests pass
- [ ] Code coverage maintained

## Common Mistakes to Avoid

### Mistake 1: Accessing Result.Value without checking
```csharp
var result = GetUser(123);
var name = result.Value.Name; // ❌ Throws if result is failure

// ✅ Correct
result.Match(
    onSuccess: user => user.Name,
    onFailure: error => "Unknown"
);
```

### Mistake 2: Not handling Result
```csharp
GetUser(123); // ❌ Result ignored

// ✅ Correct
var result = GetUser(123);
result.Switch(
    onSuccess: user => Process(user),
    onFailure: error => Log(error)
);
```

### Mistake 3: Returning null in Result
```csharp
public Result<User> FindUser(string email)
{
    var user = _repository.Find(email);
    return user; // ❌ If user is null, returns Result.Success(null)

    // ✅ Correct
    if (user is null)
        return Error.NotFoundError("User not found");
    return user;
}
```

## Questions?

If uncertain about any pattern, ask in team chat or refer to:
- `Voyager.Common.Results` library documentation
- `SOLID-PRINCIPLES.md` in this repository
- Team lead or senior developer
