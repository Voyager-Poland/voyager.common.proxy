namespace Voyager.Common.Proxy.Client.IntegrationTests.Contracts;

using Voyager.Common.Proxy.Abstractions;
using Voyager.Common.Results;

/// <summary>
/// Example service interface for integration testing.
/// Uses convention-based routing.
/// </summary>
public interface IUserService
{
    Task<Result<User>> GetUserAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<List<User>>> ListUsersAsync(string? nameFilter = null, int? limit = null, CancellationToken cancellationToken = default);

    Task<Result<User>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    Task<Result<User>> UpdateUserAsync(User user, CancellationToken cancellationToken = default);

    Task<Result> DeleteUserAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Example service interface with custom routes via attributes.
/// </summary>
[ServiceRoute("api/v2/products")]
public interface IProductService
{
    [HttpGet("{id}")]
    Task<Result<Product>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    [HttpGet]
    Task<Result<List<Product>>> SearchAsync(string? category, decimal? minPrice, CancellationToken cancellationToken = default);

    [HttpPost]
    Task<Result<Product>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);

    [HttpPut("{id}")]
    Task<Result<Product>> UpdateAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default);

    [HttpDelete("{id}")]
    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

#region DTOs

public record User
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
}

public record CreateUserRequest
{
    public string Name { get; init; } = "";
    public string Email { get; init; } = "";
}

public record Product
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public decimal Price { get; init; }
}

public record CreateProductRequest
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public decimal Price { get; init; }
}

public record UpdateProductRequest
{
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public decimal Price { get; init; }
}

#endregion
