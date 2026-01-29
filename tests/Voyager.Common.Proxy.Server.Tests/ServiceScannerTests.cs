using System.Threading;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Abstractions;
using Voyager.Common.Proxy.Server.Abstractions;
using Voyager.Common.Proxy.Server.Core;
using Voyager.Common.Results;

using ProxyHttpMethod = Voyager.Common.Proxy.Abstractions.HttpMethod;

namespace Voyager.Common.Proxy.Server.Tests;

public class ServiceScannerTests
{
    private readonly ServiceScanner _scanner = new();

    #region Test Interfaces

    public interface IUserService
    {
        Task<Result<User>> GetUserAsync(int id, CancellationToken cancellationToken);
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

    public interface IInvalidService
    {
        void SyncMethod(); // Not async - should be skipped
        Task NonResultMethod(); // Returns Task, not Task<Result>
        string StringMethod(); // Returns string, not Task
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class CreateUserRequest
    {
        public string Name { get; set; } = "";
    }

    public class UpdateUserRequest
    {
        public string Name { get; set; } = "";
    }

    public class Order
    {
        public int Id { get; set; }
    }

    public class OrderRequest
    {
        public string Product { get; set; } = "";
    }

    public interface IPaymentService
    {
        [HttpMethod(ProxyHttpMethod.Get, "get-payments/{IdBusMapCoach_RNo}")]
        Task<Result<PaymentsListResponse>> GetPaymentsListAsync(PaymentsListRequest paymentsListRequest);

        // Complex type on GET without route params - properties from query string
        Task<Result<PaymentsListResponse>> SearchPaymentsAsync(PaymentSearchRequest searchRequest);
    }

    public class PaymentSearchRequest
    {
        public int? CustomerId { get; set; }
        public string? Status { get; set; }
        public string? Language { get; set; }
    }

    public class PaymentsListRequest
    {
        public int IdBusMapCoach_RNo { get; set; }
        public string? Status { get; set; }
    }

    public class PaymentsListResponse
    {
        public List<string> Items { get; set; } = new();
    }

    #endregion

    #region ScanInterface Tests

    [Fact]
    public void ScanInterface_WithValidInterface_ReturnsEndpoints()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();

        Assert.NotEmpty(endpoints);
        Assert.Equal(5, endpoints.Count);
    }

    [Fact]
    public void ScanInterface_WithNullType_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _scanner.ScanInterface(null!));
    }

    [Fact]
    public void ScanInterface_WithNonInterface_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _scanner.ScanInterface(typeof(User)));
    }

    [Fact]
    public void ScanInterface_WithInvalidMethods_SkipsNonAsyncMethods()
    {
        var endpoints = _scanner.ScanInterface<IInvalidService>();

        Assert.Empty(endpoints);
    }

    #endregion

    #region HTTP Method Detection Tests

    [Fact]
    public void ScanInterface_GetMethod_DetectsHttpGet()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var getEndpoint = endpoints.First(e => e.Method.Name == "GetUserAsync");

        Assert.Equal("GET", getEndpoint.HttpMethod);
    }

    [Fact]
    public void ScanInterface_CreateMethod_DetectsHttpPost()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var createEndpoint = endpoints.First(e => e.Method.Name == "CreateUserAsync");

        Assert.Equal("POST", createEndpoint.HttpMethod);
    }

    [Fact]
    public void ScanInterface_UpdateMethod_DetectsHttpPut()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var updateEndpoint = endpoints.First(e => e.Method.Name == "UpdateUserAsync");

        Assert.Equal("PUT", updateEndpoint.HttpMethod);
    }

    [Fact]
    public void ScanInterface_DeleteMethod_DetectsHttpDelete()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var deleteEndpoint = endpoints.First(e => e.Method.Name == "DeleteUserAsync");

        Assert.Equal("DELETE", deleteEndpoint.HttpMethod);
    }

    [Fact]
    public void ScanInterface_SearchMethod_DetectsHttpGet()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var searchEndpoint = endpoints.First(e => e.Method.Name == "SearchUsersAsync");

        Assert.Equal("GET", searchEndpoint.HttpMethod);
    }

    [Fact]
    public void ScanInterface_WithHttpMethodAttribute_UsesAttributeMethod()
    {
        var endpoints = _scanner.ScanInterface<IOrderService>();
        var createEndpoint = endpoints.First(e => e.Method.Name == "CreateOrderAsync");

        Assert.Equal("POST", createEndpoint.HttpMethod);
    }

    #endregion

    #region Route Template Tests

    [Fact]
    public void ScanInterface_WithoutServiceRouteAttribute_UsesKebabCaseConvention()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var getEndpoint = endpoints.First(e => e.Method.Name == "GetUserAsync");

        Assert.StartsWith("/user-service/", getEndpoint.RouteTemplate);
    }

    [Fact]
    public void ScanInterface_WithServiceRouteAttribute_UsesAttributePrefix()
    {
        var endpoints = _scanner.ScanInterface<IOrderService>();
        var getEndpoint = endpoints.First(e => e.Method.Name == "GetOrderAsync");

        Assert.StartsWith("/api/v1/orders/", getEndpoint.RouteTemplate);
    }

    [Fact]
    public void ScanInterface_WithHttpMethodAttributeTemplate_UsesAttributeTemplate()
    {
        var endpoints = _scanner.ScanInterface<IOrderService>();
        var createEndpoint = endpoints.First(e => e.Method.Name == "CreateOrderAsync");

        Assert.Equal("/api/v1/orders/create", createEndpoint.RouteTemplate);
    }

    [Fact]
    public void ScanInterface_MethodNameWithAsync_RemovesAsyncSuffix()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var getEndpoint = endpoints.First(e => e.Method.Name == "GetUserAsync");

        Assert.Contains("get-user", getEndpoint.RouteTemplate);
        Assert.DoesNotContain("async", getEndpoint.RouteTemplate.ToLower());
    }

    #endregion

    #region Parameter Detection Tests

    [Fact]
    public void ScanInterface_WithCancellationToken_DetectsAsCancellationTokenSource()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var getEndpoint = endpoints.First(e => e.Method.Name == "GetUserAsync");
        var ctParam = getEndpoint.Parameters.First(p => p.Name == "cancellationToken");

        Assert.Equal(ParameterSource.CancellationToken, ctParam.Source);
    }

    [Fact]
    public void ScanInterface_WithSimpleType_DetectsAsQueryParameter()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var searchEndpoint = endpoints.First(e => e.Method.Name == "SearchUsersAsync");
        var nameParam = searchEndpoint.Parameters.First(p => p.Name == "name");

        Assert.Equal(ParameterSource.Query, nameParam.Source);
    }

    [Fact]
    public void ScanInterface_WithComplexTypeOnPost_DetectsAsBodyParameter()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var createEndpoint = endpoints.First(e => e.Method.Name == "CreateUserAsync");
        var requestParam = createEndpoint.Parameters.First(p => p.Name == "request");

        Assert.Equal(ParameterSource.Body, requestParam.Source);
    }

    [Fact]
    public void ScanInterface_WithComplexTypeOnPut_DetectsAsBodyParameter()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var updateEndpoint = endpoints.First(e => e.Method.Name == "UpdateUserAsync");
        var requestParam = updateEndpoint.Parameters.First(p => p.Name == "request");

        Assert.Equal(ParameterSource.Body, requestParam.Source);
    }

    [Fact]
    public void ScanInterface_PreservesParameterOrder()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var getEndpoint = endpoints.First(e => e.Method.Name == "GetUserAsync");

        Assert.Equal(2, getEndpoint.Parameters.Count);
        Assert.Equal("id", getEndpoint.Parameters[0].Name);
        Assert.Equal("cancellationToken", getEndpoint.Parameters[1].Name);
    }

    [Fact]
    public void ScanInterface_WithComplexTypeOnGetWithRouteParams_DetectsAsRouteAndQuery()
    {
        var endpoints = _scanner.ScanInterface<IPaymentService>();
        var getEndpoint = endpoints.First(e => e.Method.Name == "GetPaymentsListAsync");
        var requestParam = getEndpoint.Parameters.First(p => p.Name == "paymentsListRequest");

        Assert.Equal(ParameterSource.RouteAndQuery, requestParam.Source);
    }

    [Fact]
    public void ScanInterface_WithComplexTypeOnGetWithoutRouteParams_DetectsAsRouteAndQuery()
    {
        var endpoints = _scanner.ScanInterface<IPaymentService>();
        var searchEndpoint = endpoints.First(e => e.Method.Name == "SearchPaymentsAsync");
        var requestParam = searchEndpoint.Parameters.First(p => p.Name == "searchRequest");

        // Complex types on GET should use RouteAndQuery even without route placeholders
        // This allows binding properties from query string
        Assert.Equal(ParameterSource.RouteAndQuery, requestParam.Source);
    }

    #endregion

    #region Result Type Tests

    [Fact]
    public void ScanInterface_WithResultT_DetectsResultValueType()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var getEndpoint = endpoints.First(e => e.Method.Name == "GetUserAsync");

        Assert.NotNull(getEndpoint.ResultValueType);
        Assert.Equal(typeof(User), getEndpoint.ResultValueType);
    }

    [Fact]
    public void ScanInterface_WithResult_HasNullResultValueType()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var deleteEndpoint = endpoints.First(e => e.Method.Name == "DeleteUserAsync");

        Assert.Null(deleteEndpoint.ResultValueType);
    }

    [Fact]
    public void ScanInterface_WithGenericResult_DetectsInnerType()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var searchEndpoint = endpoints.First(e => e.Method.Name == "SearchUsersAsync");

        Assert.NotNull(searchEndpoint.ResultValueType);
        Assert.True(searchEndpoint.ResultValueType!.IsGenericType);
    }

    #endregion

    #region Service Type Tests

    [Fact]
    public void ScanInterface_SetsCorrectServiceType()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();

        foreach (var endpoint in endpoints)
        {
            Assert.Equal(typeof(IUserService), endpoint.ServiceType);
        }
    }

    [Fact]
    public void ScanInterface_SetsCorrectMethodInfo()
    {
        var endpoints = _scanner.ScanInterface<IUserService>();
        var getEndpoint = endpoints.First(e => e.Method.Name == "GetUserAsync");

        Assert.Equal("GetUserAsync", getEndpoint.Method.Name);
        Assert.Equal(typeof(IUserService), getEndpoint.Method.DeclaringType);
    }

    #endregion
}
