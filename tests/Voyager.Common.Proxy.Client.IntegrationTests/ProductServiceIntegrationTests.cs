namespace Voyager.Common.Proxy.Client.IntegrationTests;

using FluentAssertions;
using Voyager.Common.Proxy.Client.IntegrationTests.Contracts;
using Voyager.Common.Proxy.Client.IntegrationTests.TestServer;
using Voyager.Common.Results;
using Xunit;

/// <summary>
/// Integration tests for attribute-based routing (IProductService).
/// Verifies [ServiceRoute], [HttpGet], [HttpPost], [HttpPut], [HttpDelete] work correctly.
/// </summary>
public class ProductServiceIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IProductService _productService;

    public ProductServiceIntegrationTests()
    {
        TestServerSetup.ResetData();
        _httpClient = TestServerSetup.CreateTestClient();

        var options = new ServiceProxyOptions
        {
            BaseUrl = _httpClient.BaseAddress!
        };

        _productService = ServiceProxy<IProductService>.Create(_httpClient, options);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    #region GetById Tests (Route template: {id})

    [Fact]
    public async Task GetByIdAsync_ExistingProduct_ReturnsSuccess()
    {
        var result = await _productService.GetByIdAsync(1);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(1);
        result.Value.Name.Should().Be("Laptop");
        result.Value.Category.Should().Be("Electronics");
        result.Value.Price.Should().Be(999.99m);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingProduct_ReturnsNotFound()
    {
        var result = await _productService.GetByIdAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    #endregion

    #region Search Tests (Query parameters)

    [Fact]
    public async Task SearchAsync_NoFilters_ReturnsAllProducts()
    {
        var result = await _productService.SearchAsync(null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchAsync_ByCategory_ReturnsFilteredProducts()
    {
        var result = await _productService.SearchAsync(category: "Electronics", minPrice: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(p => p.Category.Should().Be("Electronics"));
    }

    [Fact]
    public async Task SearchAsync_ByMinPrice_ReturnsFilteredProducts()
    {
        var result = await _productService.SearchAsync(category: null, minPrice: 100m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(p => p.Price.Should().BeGreaterOrEqualTo(100m));
    }

    [Fact]
    public async Task SearchAsync_ByCategoryAndMinPrice_ReturnsCombinedResult()
    {
        var result = await _productService.SearchAsync(category: "Electronics", minPrice: 100m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("Laptop");
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsCreatedProduct()
    {
        var request = new CreateProductRequest
        {
            Name = "Keyboard",
            Category = "Electronics",
            Price = 79.99m
        };

        var result = await _productService.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().BeGreaterThan(0);
        result.Value.Name.Should().Be("Keyboard");
        result.Value.Category.Should().Be("Electronics");
        result.Value.Price.Should().Be(79.99m);
    }

    [Fact]
    public async Task CreateAsync_MissingName_ReturnsValidationError()
    {
        var request = new CreateProductRequest
        {
            Name = "",
            Category = "Electronics",
            Price = 79.99m
        };

        var result = await _productService.CreateAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    #endregion

    #region Update Tests (Route template: {id} + body)

    [Fact]
    public async Task UpdateAsync_ExistingProduct_ReturnsUpdatedProduct()
    {
        var request = new UpdateProductRequest
        {
            Name = "Gaming Laptop",
            Category = "Electronics",
            Price = 1499.99m
        };

        var result = await _productService.UpdateAsync(1, request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(1);
        result.Value.Name.Should().Be("Gaming Laptop");
        result.Value.Price.Should().Be(1499.99m);
    }

    [Fact]
    public async Task UpdateAsync_NonExistingProduct_ReturnsNotFound()
    {
        var request = new UpdateProductRequest
        {
            Name = "Ghost Product",
            Category = "Unknown",
            Price = 0m
        };

        var result = await _productService.UpdateAsync(999, request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    #endregion

    #region Delete Tests (Route template: {id})

    [Fact]
    public async Task DeleteAsync_ExistingProduct_ReturnsSuccess()
    {
        var result = await _productService.DeleteAsync(1);

        result.IsSuccess.Should().BeTrue();

        // Verify product is actually deleted
        var getResult = await _productService.GetByIdAsync(1);
        getResult.IsFailure.Should().BeTrue();
        getResult.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_NonExistingProduct_ReturnsNotFound()
    {
        var result = await _productService.DeleteAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    #endregion

    #region Full CRUD Flow Tests

    [Fact]
    public async Task FullCrudFlow_CreateReadUpdateDelete_WorksCorrectly()
    {
        // Create
        var createRequest = new CreateProductRequest
        {
            Name = "Test Monitor",
            Category = "Electronics",
            Price = 299.99m
        };
        var createResult = await _productService.CreateAsync(createRequest);
        createResult.IsSuccess.Should().BeTrue();
        var productId = createResult.Value!.Id;

        // Read
        var readResult = await _productService.GetByIdAsync(productId);
        readResult.IsSuccess.Should().BeTrue();
        readResult.Value!.Name.Should().Be("Test Monitor");

        // Update
        var updateRequest = new UpdateProductRequest
        {
            Name = "Test Monitor Pro",
            Category = "Electronics",
            Price = 399.99m
        };
        var updateResult = await _productService.UpdateAsync(productId, updateRequest);
        updateResult.IsSuccess.Should().BeTrue();
        updateResult.Value!.Name.Should().Be("Test Monitor Pro");
        updateResult.Value.Price.Should().Be(399.99m);

        // Verify update
        var verifyResult = await _productService.GetByIdAsync(productId);
        verifyResult.IsSuccess.Should().BeTrue();
        verifyResult.Value!.Name.Should().Be("Test Monitor Pro");

        // Delete
        var deleteResult = await _productService.DeleteAsync(productId);
        deleteResult.IsSuccess.Should().BeTrue();

        // Verify delete
        var finalResult = await _productService.GetByIdAsync(productId);
        finalResult.IsFailure.Should().BeTrue();
        finalResult.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    #endregion

    #region Route Template Verification

    [Fact]
    public async Task RouteTemplate_IdInPath_IsCorrectlySubstituted()
    {
        // This test verifies that [HttpGet("{id}")] correctly substitutes the id in the path
        // The test server expects /api/v2/products/{id}, not /api/v2/products?id=...

        var result = await _productService.GetByIdAsync(2);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(2);
        result.Value.Name.Should().Be("Mouse");
    }

    #endregion
}
