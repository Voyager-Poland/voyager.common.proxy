namespace Voyager.Common.Proxy.Client.IntegrationTests.TestServer;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Voyager.Common.Proxy.Client.IntegrationTests.Contracts;
using Voyager.Common.Results;

/// <summary>
/// Sets up a test server with mock implementations of service interfaces.
/// </summary>
public static class TestServerSetup
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    // In-memory data stores (thread-safe)
    private static readonly ConcurrentDictionary<int, User> Users = new();
    private static readonly ConcurrentDictionary<int, Product> Products = new();

    private static int _nextUserId = 4;
    private static int _nextProductId = 4;

    public static HttpClient CreateTestClient()
    {
        var builder = new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            MapUserServiceEndpoints(endpoints);
                            MapProductServiceEndpoints(endpoints);
                        });
                    });
            });

        var host = builder.Start();
        return host.GetTestClient();
    }

    private static void MapUserServiceEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Convention-based routes: /user-service/method-name

        // GET /user-service/get-user?id=123
        endpoints.MapGet("/user-service/get-user", async (HttpContext context) =>
        {
            var idStr = context.Request.Query["id"].ToString();
            if (!int.TryParse(idStr, out var id))
            {
                return Results.BadRequest(new { error = "Invalid id parameter" });
            }

            if (Users.TryGetValue(id, out var user))
            {
                return Results.Ok(user);
            }

            return Results.NotFound(new { error = $"User with id {id} not found" });
        });

        // GET /user-service/list-users?nameFilter=xxx&limit=10
        endpoints.MapGet("/user-service/list-users", (HttpContext context) =>
        {
            var nameFilter = context.Request.Query["nameFilter"].ToString();
            var limitStr = context.Request.Query["limit"].ToString();

            IEnumerable<User> result = Users.Values;

            if (!string.IsNullOrEmpty(nameFilter))
            {
                result = result.Where(u => u.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (int.TryParse(limitStr, out var limit) && limit > 0)
            {
                result = result.Take(limit);
            }

            return Results.Ok(result.ToList());
        });

        // POST /user-service/create-user
        endpoints.MapPost("/user-service/create-user", async (HttpContext context) =>
        {
            var request = await JsonSerializer.DeserializeAsync<CreateUserRequest>(
                context.Request.Body, JsonOptions);

            if (request is null || string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Name is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { error = "Email is required" });
            }

            var user = new User
            {
                Id = _nextUserId++,
                Name = request.Name,
                Email = request.Email
            };

            Users[user.Id] = user;
            return Results.Created($"/user-service/get-user?id={user.Id}", user);
        });

        // PUT /user-service/update-user
        endpoints.MapPut("/user-service/update-user", async (HttpContext context) =>
        {
            var user = await JsonSerializer.DeserializeAsync<User>(
                context.Request.Body, JsonOptions);

            if (user is null)
            {
                return Results.BadRequest(new { error = "Invalid user data" });
            }

            if (!Users.ContainsKey(user.Id))
            {
                return Results.NotFound(new { error = $"User with id {user.Id} not found" });
            }

            Users[user.Id] = user;
            return Results.Ok(user);
        });

        // DELETE /user-service/delete-user?id=123
        endpoints.MapDelete("/user-service/delete-user", (HttpContext context) =>
        {
            var idStr = context.Request.Query["id"].ToString();
            if (!int.TryParse(idStr, out var id))
            {
                return Results.BadRequest(new { error = "Invalid id parameter" });
            }

            if (!Users.ContainsKey(id))
            {
                return Results.NotFound(new { error = $"User with id {id} not found" });
            }

            Users.TryRemove(id, out _);
            return Results.NoContent();
        });
    }

    private static void MapProductServiceEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Attribute-based routes: /api/v2/products/...

        // GET /api/v2/products/{id}
        endpoints.MapGet("/api/v2/products/{id:int}", (int id) =>
        {
            if (Products.TryGetValue(id, out var product))
            {
                return Results.Ok(product);
            }

            return Results.NotFound(new { error = $"Product with id {id} not found" });
        });

        // GET /api/v2/products/search?category=xxx&minPrice=10
        // Note: Client uses convention for [HttpGet] without template: method name -> kebab-case
        endpoints.MapGet("/api/v2/products/search", (HttpContext context) =>
        {
            var category = context.Request.Query["category"].ToString();
            var minPriceStr = context.Request.Query["minPrice"].ToString();

            IEnumerable<Product> result = Products.Values;

            if (!string.IsNullOrEmpty(category))
            {
                result = result.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            }

            if (decimal.TryParse(minPriceStr, out var minPrice))
            {
                result = result.Where(p => p.Price >= minPrice);
            }

            return Results.Ok(result.ToList());
        });

        // POST /api/v2/products/create
        // Note: Client uses convention for [HttpPost] without template
        endpoints.MapPost("/api/v2/products/create", async (HttpContext context) =>
        {
            var request = await JsonSerializer.DeserializeAsync<CreateProductRequest>(
                context.Request.Body, JsonOptions);

            if (request is null || string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Name is required" });
            }

            var product = new Product
            {
                Id = _nextProductId++,
                Name = request.Name,
                Category = request.Category,
                Price = request.Price
            };

            Products[product.Id] = product;
            return Results.Created($"/api/v2/products/{product.Id}", product);
        });

        // PUT /api/v2/products/{id}
        endpoints.MapPut("/api/v2/products/{id:int}", async (int id, HttpContext context) =>
        {
            var request = await JsonSerializer.DeserializeAsync<UpdateProductRequest>(
                context.Request.Body, JsonOptions);

            if (request is null)
            {
                return Results.BadRequest(new { error = "Invalid product data" });
            }

            if (!Products.ContainsKey(id))
            {
                return Results.NotFound(new { error = $"Product with id {id} not found" });
            }

            var product = new Product
            {
                Id = id,
                Name = request.Name,
                Category = request.Category,
                Price = request.Price
            };

            Products[id] = product;
            return Results.Ok(product);
        });

        // DELETE /api/v2/products/{id}
        endpoints.MapDelete("/api/v2/products/{id:int}", (int id) =>
        {
            if (!Products.ContainsKey(id))
            {
                return Results.NotFound(new { error = $"Product with id {id} not found" });
            }

            Products.TryRemove(id, out _);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// Resets in-memory data to initial state between tests.
    /// </summary>
    public static void ResetData()
    {
        Users.Clear();
        Users.TryAdd(1, new User { Id = 1, Name = "John Doe", Email = "john@example.com" });
        Users.TryAdd(2, new User { Id = 2, Name = "Jane Smith", Email = "jane@example.com" });
        Users.TryAdd(3, new User { Id = 3, Name = "Bob Wilson", Email = "bob@example.com" });
        Interlocked.Exchange(ref _nextUserId, 4);

        Products.Clear();
        Products.TryAdd(1, new Product { Id = 1, Name = "Laptop", Category = "Electronics", Price = 999.99m });
        Products.TryAdd(2, new Product { Id = 2, Name = "Mouse", Category = "Electronics", Price = 29.99m });
        Products.TryAdd(3, new Product { Id = 3, Name = "Desk Chair", Category = "Furniture", Price = 199.99m });
        Interlocked.Exchange(ref _nextProductId, 4);
    }
}
