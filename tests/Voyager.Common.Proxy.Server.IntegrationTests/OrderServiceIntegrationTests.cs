using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Voyager.Common.Proxy.Server.IntegrationTests;

[Collection("Server")]
public class OrderServiceIntegrationTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public OrderServiceIntegrationTests(ServerTestFixture fixture)
    {
        _client = fixture.Client;
    }

    #region Custom Route Tests (ServiceRoute attribute)

    [Fact]
    public async Task GetOrder_UsesCustomServiceRoute()
    {
        // Act - uses /api/orders prefix from ServiceRoute attribute, id is query param
        var response = await _client.GetAsync("/api/orders/get-order?id=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(1, order.Id);
    }

    [Fact]
    public async Task GetOrdersByUser_UsesCustomRouteTemplate()
    {
        // Act - uses custom route template "user/{userId}"
        var response = await _client.GetAsync("/api/orders/user/1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orders = await response.Content.ReadFromJsonAsync<Order[]>(JsonOptions);
        Assert.NotNull(orders);
        Assert.All(orders, o => Assert.Equal(1, o.UserId));
    }

    [Fact]
    public async Task UpdateOrderStatus_UsesCustomRouteWithQueryParameter()
    {
        // Act - uses custom route template "{id}/status"
        var response = await _client.PutAsync("/api/orders/1/status?status=Processing", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(1, order.Id);
        Assert.Equal(OrderStatus.Processing, order.Status);
    }

    #endregion

    #region CRUD Tests

    [Fact]
    public async Task GetOrder_ExistingId_ReturnsOrder()
    {
        // Act - id is query parameter
        var response = await _client.GetAsync("/api/orders/get-order?id=1");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(1, order.Id);
        Assert.Equal(99.99m, order.Total);
    }

    [Fact]
    public async Task GetOrder_NonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/orders/get-order?id=999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_ReturnsCreatedOrder()
    {
        // Arrange
        var request = new CreateOrderRequest(1, 199.99m);

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders/create-order", request, JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(1, order.UserId);
        Assert.Equal(199.99m, order.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task CreateOrder_InvalidTotal_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateOrderRequest(1, -10m);

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders/create-order", request, JsonOptions);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_ExistingId_ReturnsNoContent()
    {
        // First create an order to delete
        var createRequest = new CreateOrderRequest(1, 50m);
        var createResponse = await _client.PostAsJsonAsync("/api/orders/create-order", createRequest, JsonOptions);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<Order>(JsonOptions);

        // Act - id is query parameter
        var response = await _client.DeleteAsync($"/api/orders/delete-order?id={createdOrder!.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteOrder_NonExistingId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/orders/delete-order?id=9999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region Enum Parameter Tests

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    [InlineData(OrderStatus.Delivered)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task UpdateOrderStatus_AllEnumValues_Work(OrderStatus status)
    {
        // Arrange - create a fresh order for each test
        var createRequest = new CreateOrderRequest(1, 100m);
        var createResponse = await _client.PostAsJsonAsync("/api/orders/create-order", createRequest, JsonOptions);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<Order>(JsonOptions);

        // Act - uses custom route template "{id}/status"
        var response = await _client.PutAsync($"/api/orders/{createdOrder!.Id}/status?status={status}", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(status, order.Status);
    }

    [Fact]
    public async Task UpdateOrderStatus_CaseInsensitiveEnum_Works()
    {
        // Arrange
        var createRequest = new CreateOrderRequest(1, 100m);
        var createResponse = await _client.PostAsJsonAsync("/api/orders/create-order", createRequest, JsonOptions);
        var createdOrder = await createResponse.Content.ReadFromJsonAsync<Order>(JsonOptions);

        // Act - use lowercase enum value
        var response = await _client.PutAsync($"/api/orders/{createdOrder!.Id}/status?status=shipped", null);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<Order>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    #endregion
}
