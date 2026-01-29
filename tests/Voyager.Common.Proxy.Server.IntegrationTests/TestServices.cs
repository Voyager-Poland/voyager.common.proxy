using Voyager.Common.Proxy.Abstractions;
using Voyager.Common.Results;
using ProxyHttpMethod = Voyager.Common.Proxy.Abstractions.HttpMethod;

namespace Voyager.Common.Proxy.Server.IntegrationTests;

#region DTOs

public record User(int Id, string Name, string Email);

public record CreateUserRequest(string Name, string Email);

public record UpdateUserRequest(string Name, string Email);

public record Order(int Id, int UserId, decimal Total, OrderStatus Status);

public record CreateOrderRequest(int UserId, decimal Total);

public enum OrderStatus { Pending, Processing, Shipped, Delivered, Cancelled }

#endregion

#region Service Interfaces

public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id, CancellationToken cancellationToken);
    Task<Result<IEnumerable<User>>> GetAllUsersAsync(CancellationToken cancellationToken);
    Task<Result<IEnumerable<User>>> SearchUsersAsync(string? name, int? limit, CancellationToken cancellationToken);
    Task<Result<User>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task<Result<User>> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken);
    Task<Result> DeleteUserAsync(int id, CancellationToken cancellationToken);
}

[ServiceRoute("api/orders")]
public interface IOrderService
{
    Task<Result<Order>> GetOrderAsync(int id, CancellationToken cancellationToken);

    [HttpMethod(ProxyHttpMethod.Get, "user/{userId}")]
    Task<Result<IEnumerable<Order>>> GetOrdersByUserAsync(int userId, CancellationToken cancellationToken);

    Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken);

    [HttpMethod(ProxyHttpMethod.Put, "{id}/status")]
    Task<Result<Order>> UpdateOrderStatusAsync(int id, OrderStatus status, CancellationToken cancellationToken);

    Task<Result> DeleteOrderAsync(int id, CancellationToken cancellationToken);
}

#endregion

#region Service Implementations

public class InMemoryUserService : IUserService
{
    private readonly Dictionary<int, User> _users = new()
    {
        [1] = new User(1, "Alice", "alice@example.com"),
        [2] = new User(2, "Bob", "bob@example.com"),
        [3] = new User(3, "Charlie", "charlie@example.com")
    };
    private int _nextId = 4;

    public Task<Result<User>> GetUserAsync(int id, CancellationToken cancellationToken)
    {
        if (_users.TryGetValue(id, out var user))
        {
            return Task.FromResult(Result<User>.Success(user));
        }
        return Task.FromResult(Result<User>.Failure(Error.NotFoundError($"User {id} not found")));
    }

    public Task<Result<IEnumerable<User>>> GetAllUsersAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<IEnumerable<User>>.Success(_users.Values.AsEnumerable()));
    }

    public Task<Result<IEnumerable<User>>> SearchUsersAsync(string? name, int? limit, CancellationToken cancellationToken)
    {
        var query = _users.Values.AsEnumerable();

        if (!string.IsNullOrEmpty(name))
        {
            query = query.Where(u => u.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return Task.FromResult(Result<IEnumerable<User>>.Success(query));
    }

    public Task<Result<User>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Task.FromResult(Result<User>.Failure(Error.ValidationError("Name is required")));
        }

        var user = new User(_nextId++, request.Name, request.Email);
        _users[user.Id] = user;
        return Task.FromResult(Result<User>.Success(user));
    }

    public Task<Result<User>> UpdateUserAsync(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        if (!_users.TryGetValue(id, out var existing))
        {
            return Task.FromResult(Result<User>.Failure(Error.NotFoundError($"User {id} not found")));
        }

        var updated = existing with { Name = request.Name, Email = request.Email };
        _users[id] = updated;
        return Task.FromResult(Result<User>.Success(updated));
    }

    public Task<Result> DeleteUserAsync(int id, CancellationToken cancellationToken)
    {
        if (!_users.Remove(id))
        {
            return Task.FromResult(Result.Failure(Error.NotFoundError($"User {id} not found")));
        }
        return Task.FromResult(Result.Success());
    }
}

public class InMemoryOrderService : IOrderService
{
    private readonly Dictionary<int, Order> _orders = new()
    {
        [1] = new Order(1, 1, 99.99m, OrderStatus.Pending),
        [2] = new Order(2, 1, 149.99m, OrderStatus.Shipped),
        [3] = new Order(3, 2, 49.99m, OrderStatus.Delivered)
    };
    private int _nextId = 4;

    public Task<Result<Order>> GetOrderAsync(int id, CancellationToken cancellationToken)
    {
        if (_orders.TryGetValue(id, out var order))
        {
            return Task.FromResult(Result<Order>.Success(order));
        }
        return Task.FromResult(Result<Order>.Failure(Error.NotFoundError($"Order {id} not found")));
    }

    public Task<Result<IEnumerable<Order>>> GetOrdersByUserAsync(int userId, CancellationToken cancellationToken)
    {
        var orders = _orders.Values.Where(o => o.UserId == userId);
        return Task.FromResult(Result<IEnumerable<Order>>.Success(orders));
    }

    public Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Total <= 0)
        {
            return Task.FromResult(Result<Order>.Failure(Error.ValidationError("Total must be positive")));
        }

        var order = new Order(_nextId++, request.UserId, request.Total, OrderStatus.Pending);
        _orders[order.Id] = order;
        return Task.FromResult(Result<Order>.Success(order));
    }

    public Task<Result<Order>> UpdateOrderStatusAsync(int id, OrderStatus status, CancellationToken cancellationToken)
    {
        if (!_orders.TryGetValue(id, out var existing))
        {
            return Task.FromResult(Result<Order>.Failure(Error.NotFoundError($"Order {id} not found")));
        }

        var updated = existing with { Status = status };
        _orders[id] = updated;
        return Task.FromResult(Result<Order>.Success(updated));
    }

    public Task<Result> DeleteOrderAsync(int id, CancellationToken cancellationToken)
    {
        if (!_orders.Remove(id))
        {
            return Task.FromResult(Result.Failure(Error.NotFoundError($"Order {id} not found")));
        }
        return Task.FromResult(Result.Success());
    }
}

#endregion
