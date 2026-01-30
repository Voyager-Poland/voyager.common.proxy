using Voyager.Common.Proxy.Abstractions;
using Voyager.Common.Proxy.Server.Swagger.Core;
using Voyager.Common.Proxy.Server.Swagger.Core.Models;
using Voyager.Common.Results;

using ProxyHttpMethod = Voyager.Common.Proxy.Abstractions.HttpMethod;

namespace Voyager.Common.Proxy.Server.Swagger.Core.Tests;

public class ServiceProxySwaggerGeneratorTests
{
    private readonly ServiceProxySwaggerGenerator _generator = new();

    #region Test Interfaces and Types

    public interface IUserService
    {
        Task<Result<User>> GetUserAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<User>> CreateUserAsync(CreateUserRequest request);
        Task<Result<IEnumerable<User>>> SearchUsersAsync(string? name, int? limit);
        Task<Result> DeleteUserAsync(int id);
        Task<Result<User>> UpdateUserAsync(int id, UpdateUserRequest request);
    }

    [ServiceRoute("api/v1/orders")]
    public interface IOrderService
    {
        Task<Result<Order>> GetOrderAsync(int id);

        [HttpMethod(ProxyHttpMethod.Post, "create")]
        Task<Result<Order>> CreateOrderAsync(OrderRequest request);
    }

    public interface IPaymentService
    {
        [HttpMethod(ProxyHttpMethod.Get, "payments/{customerId}")]
        Task<Result<PaymentResponse>> GetPaymentsAsync(PaymentRequest request);
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class CreateUserRequest
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }

    public class UpdateUserRequest
    {
        public string Name { get; set; } = "";
    }

    public class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
    }

    public class OrderRequest
    {
        public string Product { get; set; } = "";
        public int Quantity { get; set; }
    }

    public class PaymentRequest
    {
        public int CustomerId { get; set; }
        public string? Status { get; set; }
    }

    public class PaymentResponse
    {
        public List<Payment> Payments { get; set; } = new();
    }

    public class Payment
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
    }

    #endregion

    #region GeneratePaths Tests

    [Fact]
    public void GeneratePaths_WithValidInterface_ReturnsPathDefinitions()
    {
        var paths = _generator.GeneratePaths<IUserService>();

        Assert.NotEmpty(paths);
        Assert.Equal(5, paths.Count);
    }

    [Fact]
    public void GeneratePaths_ReturnsCorrectHttpMethods()
    {
        var paths = _generator.GeneratePaths<IUserService>();

        var getPath = paths.First(p => p.Operation.OperationId == "GetUser");
        var createPath = paths.First(p => p.Operation.OperationId == "CreateUser");
        var updatePath = paths.First(p => p.Operation.OperationId == "UpdateUser");
        var deletePath = paths.First(p => p.Operation.OperationId == "DeleteUser");

        Assert.Equal("GET", getPath.HttpMethod);
        Assert.Equal("POST", createPath.HttpMethod);
        Assert.Equal("PUT", updatePath.HttpMethod);
        Assert.Equal("DELETE", deletePath.HttpMethod);
    }

    [Fact]
    public void GeneratePaths_ReturnsCorrectRoutePaths()
    {
        var paths = _generator.GeneratePaths<IUserService>();

        var getPath = paths.First(p => p.Operation.OperationId == "GetUser");

        Assert.StartsWith("/user-service/", getPath.Path);
        Assert.Contains("get-user", getPath.Path);
    }

    [Fact]
    public void GeneratePaths_WithServiceRouteAttribute_UsesCustomPrefix()
    {
        var paths = _generator.GeneratePaths<IOrderService>();

        var getPath = paths.First(p => p.Operation.OperationId == "GetOrder");

        Assert.StartsWith("/api/v1/orders/", getPath.Path);
    }

    #endregion

    #region Operation Tests

    [Fact]
    public void GeneratePaths_OperationId_RemovesAsyncSuffix()
    {
        var paths = _generator.GeneratePaths<IUserService>();

        foreach (var path in paths)
        {
            Assert.DoesNotContain("Async", path.Operation.OperationId);
        }
    }

    [Fact]
    public void GeneratePaths_Tags_ContainsServiceName()
    {
        var paths = _generator.GeneratePaths<IUserService>();

        foreach (var path in paths)
        {
            Assert.Contains("UserService", path.Operation.Tags);
        }
    }

    [Fact]
    public void GeneratePaths_Tags_RemovesInterfacePrefix()
    {
        var paths = _generator.GeneratePaths<IUserService>();

        foreach (var path in paths)
        {
            Assert.DoesNotContain("IUserService", path.Operation.Tags);
        }
    }

    #endregion

    #region Parameter Tests

    [Fact]
    public void GeneratePaths_QueryParameters_AreCamelCase()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var getPath = paths.First(p => p.Operation.OperationId == "GetUser");

        var idParam = getPath.Operation.Parameters.First(p => p.Name == "id");

        Assert.NotNull(idParam);
        Assert.Equal(ParameterLocation.Query, idParam.Location);
    }

    [Fact]
    public void GeneratePaths_SkipsCancellationTokenParameter()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var getPath = paths.First(p => p.Operation.OperationId == "GetUser");

        Assert.DoesNotContain(getPath.Operation.Parameters, p => p.Name == "cancellationToken");
    }

    [Fact]
    public void GeneratePaths_PostWithComplexType_HasRequestBody()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var createPath = paths.First(p => p.Operation.OperationId == "CreateUser");

        Assert.NotNull(createPath.Operation.RequestBody);
        Assert.Equal("application/json", createPath.Operation.RequestBody.ContentType);
        Assert.True(createPath.Operation.RequestBody.Required);
    }

    [Fact]
    public void GeneratePaths_PostWithComplexType_NoBodyInParameters()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var createPath = paths.First(p => p.Operation.OperationId == "CreateUser");

        // Body parameters should not be in the Parameters list
        Assert.Empty(createPath.Operation.Parameters);
    }

    [Fact]
    public void GeneratePaths_GetWithRouteAndQuery_ExpandsProperties()
    {
        var paths = _generator.GeneratePaths<IPaymentService>();
        var getPath = paths.First(p => p.Operation.OperationId == "GetPayments");

        // Should have customerId as path param and status as query param
        var customerIdParam = getPath.Operation.Parameters.FirstOrDefault(p => p.Name == "customerId");
        var statusParam = getPath.Operation.Parameters.FirstOrDefault(p => p.Name == "status");

        Assert.NotNull(customerIdParam);
        Assert.NotNull(statusParam);
        Assert.Equal(ParameterLocation.Path, customerIdParam.Location);
        Assert.Equal(ParameterLocation.Query, statusParam.Location);
    }

    [Fact]
    public void GeneratePaths_RequiredParameter_IsMarkedRequired()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var deletePath = paths.First(p => p.Operation.OperationId == "DeleteUser");

        var idParam = deletePath.Operation.Parameters.First(p => p.Name == "id");

        Assert.True(idParam.Required);
    }

    [Fact]
    public void GeneratePaths_ParametersWithoutDefaultValues_MarkedAsRequired()
    {
        // Note: Both string? and int? method parameters without default values are marked as required
        // because the parameter doesn't have a default value (IsOptional = false in reflection)
        // To make them optional, you need to provide a default value like: int? limit = null
        var paths = _generator.GeneratePaths<IUserService>();
        var searchPath = paths.First(p => p.Operation.OperationId == "SearchUsers");

        var nameParam = searchPath.Operation.Parameters.First(p => p.Name == "name");
        var limitParam = searchPath.Operation.Parameters.First(p => p.Name == "limit");

        Assert.True(nameParam.Required);
        Assert.True(limitParam.Required);
    }

    #endregion

    #region Response Tests

    [Fact]
    public void GeneratePaths_WithResultT_Has200ResponseWithSchema()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var getPath = paths.First(p => p.Operation.OperationId == "GetUser");

        var successResponse = getPath.Operation.Responses.First(r => r.StatusCode == 200);

        Assert.NotNull(successResponse.Schema);
        Assert.Equal("application/json", successResponse.ContentType);
    }

    [Fact]
    public void GeneratePaths_WithResultT_UnwrapsResultType()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var getPath = paths.First(p => p.Operation.OperationId == "GetUser");

        var successResponse = getPath.Operation.Responses.First(r => r.StatusCode == 200);

        // Should be a reference to User, not Result<User>
        Assert.True(successResponse.Schema!.IsReference);
        Assert.Contains("User", successResponse.Schema.Reference);
        Assert.DoesNotContain("Result", successResponse.Schema.Reference);
    }

    [Fact]
    public void GeneratePaths_WithResultVoid_Has204Response()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var deletePath = paths.First(p => p.Operation.OperationId == "DeleteUser");

        var noContentResponse = deletePath.Operation.Responses.First(r => r.StatusCode == 204);

        Assert.Null(noContentResponse.Schema);
        Assert.Equal("No Content", noContentResponse.Description);
    }

    [Fact]
    public void GeneratePaths_HasStandardErrorResponses()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var getPath = paths.First(p => p.Operation.OperationId == "GetUser");

        var statusCodes = getPath.Operation.Responses.Select(r => r.StatusCode).ToList();

        Assert.Contains(400, statusCodes);
        Assert.Contains(401, statusCodes);
        Assert.Contains(403, statusCodes);
        Assert.Contains(404, statusCodes);
        Assert.Contains(409, statusCodes);
        Assert.Contains(500, statusCodes);
    }

    [Fact]
    public void GeneratePaths_ErrorResponses_HaveErrorSchema()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var getPath = paths.First(p => p.Operation.OperationId == "GetUser");

        var badRequestResponse = getPath.Operation.Responses.First(r => r.StatusCode == 400);

        Assert.NotNull(badRequestResponse.Schema);
        Assert.NotNull(badRequestResponse.Schema.Properties);
        Assert.True(badRequestResponse.Schema.Properties.ContainsKey("error"));
    }

    #endregion

    #region Schema Generation Integration Tests

    [Fact]
    public void GeneratePaths_ComplexTypes_AddedToSchemaGenerator()
    {
        _generator.GeneratePaths<IUserService>();

        Assert.True(_generator.SchemaGenerator.ComponentSchemas.ContainsKey("User"));
        Assert.True(_generator.SchemaGenerator.ComponentSchemas.ContainsKey("CreateUserRequest"));
        Assert.True(_generator.SchemaGenerator.ComponentSchemas.ContainsKey("UpdateUserRequest"));
    }

    [Fact]
    public void GeneratePaths_CollectionResponse_UnwrapsToArrayOfType()
    {
        var paths = _generator.GeneratePaths<IUserService>();
        var searchPath = paths.First(p => p.Operation.OperationId == "SearchUsers");

        var successResponse = searchPath.Operation.Responses.First(r => r.StatusCode == 200);

        Assert.NotNull(successResponse.Schema);
        Assert.Equal("array", successResponse.Schema.Type);
        Assert.NotNull(successResponse.Schema.Items);
    }

    #endregion

    #region Custom Schema Generator Tests

    [Fact]
    public void Constructor_WithCustomSchemaGenerator_UsesProvidedGenerator()
    {
        var customSchemaGenerator = new SchemaGenerator();
        var generator = new ServiceProxySwaggerGenerator(customSchemaGenerator);

        Assert.Same(customSchemaGenerator, generator.SchemaGenerator);
    }

    [Fact]
    public void Constructor_WithNullSchemaGenerator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceProxySwaggerGenerator(null!));
    }

    #endregion
}
