using System.Text;
using System.Text.Json;
using System.Threading;
using Voyager.Common.Proxy.Server.Abstractions;
using Voyager.Common.Proxy.Server.Core;

namespace Voyager.Common.Proxy.Server.Tests;

public class ParameterBinderTests
{
    private readonly ParameterBinder _binder = new();

    #region Test Helpers

    private class TestRequestContext : IRequestContext
    {
        public string HttpMethod { get; set; } = "GET";
        public string Path { get; set; } = "/";
        public IReadOnlyDictionary<string, string> RouteValues { get; set; } = new Dictionary<string, string>();
        public IReadOnlyDictionary<string, string> QueryParameters { get; set; } = new Dictionary<string, string>();
        public Stream Body { get; set; } = Stream.Null;
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    }

    private static EndpointDescriptor CreateEndpoint(params ParameterDescriptor[] parameters)
    {
        return new EndpointDescriptor(
            serviceType: typeof(object),
            method: typeof(object).GetMethod("ToString")!,
            httpMethod: "GET",
            routeTemplate: "/test",
            parameters: parameters.ToList(),
            returnType: typeof(object),
            resultValueType: null);
    }

    private static Stream CreateJsonBody<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    #endregion

    #region Route Parameter Binding Tests

    [Fact]
    public async Task BindParameters_FromRoute_BindsIntegerValue()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["id"] = "42" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("id", typeof(int), ParameterSource.Route, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(42, values[0]);
    }

    [Fact]
    public async Task BindParameters_FromRoute_BindsStringValue()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["name"] = "john" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("name", typeof(string), ParameterSource.Route, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal("john", values[0]);
    }

    [Fact]
    public async Task BindParameters_FromRoute_BindsGuidValue()
    {
        var guid = Guid.NewGuid();
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["id"] = guid.ToString() }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("id", typeof(Guid), ParameterSource.Route, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(guid, values[0]);
    }

    [Fact]
    public async Task BindParameters_FromRoute_MissingRequired_ThrowsArgumentException()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string>()
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("id", typeof(int), ParameterSource.Route, false, null));

        await Assert.ThrowsAsync<ArgumentException>(() => _binder.BindParametersAsync(context, endpoint));
    }

    [Fact]
    public async Task BindParameters_FromRoute_MissingOptional_ReturnsDefaultValue()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string>()
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("id", typeof(int), ParameterSource.Route, true, 99));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(99, values[0]);
    }

    #endregion

    #region Query Parameter Binding Tests

    [Fact]
    public async Task BindParameters_FromQuery_BindsIntegerValue()
    {
        var context = new TestRequestContext
        {
            QueryParameters = new Dictionary<string, string> { ["limit"] = "10" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("limit", typeof(int), ParameterSource.Query, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(10, values[0]);
    }

    [Fact]
    public async Task BindParameters_FromQuery_BindsNullableIntValue()
    {
        var context = new TestRequestContext
        {
            QueryParameters = new Dictionary<string, string> { ["limit"] = "10" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("limit", typeof(int?), ParameterSource.Query, true, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(10, values[0]);
    }

    [Fact]
    public async Task BindParameters_FromQuery_MissingNullable_ReturnsNull()
    {
        var context = new TestRequestContext
        {
            QueryParameters = new Dictionary<string, string>()
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("limit", typeof(int?), ParameterSource.Query, true, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Null(values[0]);
    }

    [Fact]
    public async Task BindParameters_FromQuery_BindsBooleanValue()
    {
        var context = new TestRequestContext
        {
            QueryParameters = new Dictionary<string, string> { ["active"] = "true" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("active", typeof(bool), ParameterSource.Query, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(true, values[0]);
    }

    [Fact]
    public async Task BindParameters_FromQuery_BindsDecimalValue()
    {
        var context = new TestRequestContext
        {
            QueryParameters = new Dictionary<string, string> { ["price"] = "19.99" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("price", typeof(decimal), ParameterSource.Query, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(19.99m, values[0]);
    }

    [Fact]
    public async Task BindParameters_FromQuery_CaseInsensitive()
    {
        var context = new TestRequestContext
        {
            QueryParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LIMIT"] = "10"
            }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("limit", typeof(int), ParameterSource.Query, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(10, values[0]);
    }

    #endregion

    #region Body Parameter Binding Tests

    public class CreateUserRequest
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }

    [Fact]
    public async Task BindParameters_FromBody_BindsComplexType()
    {
        var request = new CreateUserRequest { Name = "John", Age = 30 };
        var context = new TestRequestContext
        {
            Body = CreateJsonBody(request)
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("request", typeof(CreateUserRequest), ParameterSource.Body, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        var boundRequest = Assert.IsType<CreateUserRequest>(values[0]);
        Assert.Equal("John", boundRequest.Name);
        Assert.Equal(30, boundRequest.Age);
    }

    [Fact]
    public async Task BindParameters_FromBody_EmptyBody_ReturnsNull()
    {
        var context = new TestRequestContext
        {
            Body = Stream.Null
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("request", typeof(CreateUserRequest), ParameterSource.Body, true, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Null(values[0]);
    }

    [Fact]
    public async Task BindParameters_FromBody_InvalidJson_ThrowsArgumentException()
    {
        var context = new TestRequestContext
        {
            Body = new MemoryStream(Encoding.UTF8.GetBytes("not valid json"))
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("request", typeof(CreateUserRequest), ParameterSource.Body, false, null));

        await Assert.ThrowsAsync<ArgumentException>(() => _binder.BindParametersAsync(context, endpoint));
    }

    [Fact]
    public async Task BindParameters_FromBody_UsesCamelCaseNaming()
    {
        var json = """{"name": "John", "age": 30}""";
        var context = new TestRequestContext
        {
            Body = new MemoryStream(Encoding.UTF8.GetBytes(json))
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("request", typeof(CreateUserRequest), ParameterSource.Body, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        var boundRequest = Assert.IsType<CreateUserRequest>(values[0]);
        Assert.Equal("John", boundRequest.Name);
        Assert.Equal(30, boundRequest.Age);
    }

    #endregion

    #region CancellationToken Binding Tests

    [Fact]
    public async Task BindParameters_CancellationToken_BindsFromContext()
    {
        var cts = new CancellationTokenSource();
        var context = new TestRequestContext
        {
            CancellationToken = cts.Token
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("cancellationToken", typeof(CancellationToken), ParameterSource.CancellationToken, true, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(cts.Token, values[0]);
    }

    #endregion

    #region Multiple Parameter Binding Tests

    [Fact]
    public async Task BindParameters_MultipleParameters_BindsAll()
    {
        var cts = new CancellationTokenSource();
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["id"] = "42" },
            QueryParameters = new Dictionary<string, string> { ["includeDetails"] = "true" },
            CancellationToken = cts.Token
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("id", typeof(int), ParameterSource.Route, false, null),
            new ParameterDescriptor("includeDetails", typeof(bool), ParameterSource.Query, true, false),
            new ParameterDescriptor("cancellationToken", typeof(CancellationToken), ParameterSource.CancellationToken, true, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Equal(3, values.Length);
        Assert.Equal(42, values[0]);
        Assert.Equal(true, values[1]);
        Assert.Equal(cts.Token, values[2]);
    }

    [Fact]
    public async Task BindParameters_PreservesParameterOrder()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["id"] = "1" },
            QueryParameters = new Dictionary<string, string> { ["name"] = "test", ["count"] = "5" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("name", typeof(string), ParameterSource.Query, false, null),
            new ParameterDescriptor("id", typeof(int), ParameterSource.Route, false, null),
            new ParameterDescriptor("count", typeof(int), ParameterSource.Query, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Equal(3, values.Length);
        Assert.Equal("test", values[0]);
        Assert.Equal(1, values[1]);
        Assert.Equal(5, values[2]);
    }

    #endregion

    #region Enum Binding Tests

    public enum Status { Active, Inactive, Pending }

    [Fact]
    public async Task BindParameters_FromQuery_BindsEnumValue()
    {
        var context = new TestRequestContext
        {
            QueryParameters = new Dictionary<string, string> { ["status"] = "Active" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("status", typeof(Status), ParameterSource.Query, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(Status.Active, values[0]);
    }

    [Fact]
    public async Task BindParameters_FromQuery_BindsEnumValueCaseInsensitive()
    {
        var context = new TestRequestContext
        {
            QueryParameters = new Dictionary<string, string> { ["status"] = "PENDING" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("status", typeof(Status), ParameterSource.Query, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        Assert.Equal(Status.Pending, values[0]);
    }

    #endregion

    #region RouteAndQuery Parameter Binding Tests

    public class PaymentsListRequest
    {
        public int IdBusMapCoach_RNo { get; set; }
        public string? Status { get; set; }
        public int? Limit { get; set; }
    }

    [Fact]
    public async Task BindParameters_FromRouteAndQuery_BindsRouteValue()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["IdBusMapCoach_RNo"] = "123" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("request", typeof(PaymentsListRequest), ParameterSource.RouteAndQuery, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        var request = Assert.IsType<PaymentsListRequest>(values[0]);
        Assert.Equal(123, request.IdBusMapCoach_RNo);
    }

    [Fact]
    public async Task BindParameters_FromRouteAndQuery_BindsQueryValue()
    {
        var context = new TestRequestContext
        {
            QueryParameters = new Dictionary<string, string> { ["Status"] = "Active", ["Limit"] = "10" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("request", typeof(PaymentsListRequest), ParameterSource.RouteAndQuery, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        var request = Assert.IsType<PaymentsListRequest>(values[0]);
        Assert.Equal("Active", request.Status);
        Assert.Equal(10, request.Limit);
    }

    [Fact]
    public async Task BindParameters_FromRouteAndQuery_RouteValueTakesPrecedence()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["IdBusMapCoach_RNo"] = "100" },
            QueryParameters = new Dictionary<string, string> { ["IdBusMapCoach_RNo"] = "999" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("request", typeof(PaymentsListRequest), ParameterSource.RouteAndQuery, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        var request = Assert.IsType<PaymentsListRequest>(values[0]);
        Assert.Equal(100, request.IdBusMapCoach_RNo); // Route value, not query value
    }

    [Fact]
    public async Task BindParameters_FromRouteAndQuery_BindsMixedValues()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["IdBusMapCoach_RNo"] = "42" },
            QueryParameters = new Dictionary<string, string> { ["Status"] = "Pending", ["Limit"] = "5" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("request", typeof(PaymentsListRequest), ParameterSource.RouteAndQuery, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        var request = Assert.IsType<PaymentsListRequest>(values[0]);
        Assert.Equal(42, request.IdBusMapCoach_RNo);
        Assert.Equal("Pending", request.Status);
        Assert.Equal(5, request.Limit);
    }

    [Fact]
    public async Task BindParameters_FromRouteAndQuery_MissingValues_LeavesDefaults()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["IdBusMapCoach_RNo"] = "1" }
        };
        var endpoint = CreateEndpoint(
            new ParameterDescriptor("request", typeof(PaymentsListRequest), ParameterSource.RouteAndQuery, false, null));

        var values = await _binder.BindParametersAsync(context, endpoint);

        Assert.Single(values);
        var request = Assert.IsType<PaymentsListRequest>(values[0]);
        Assert.Equal(1, request.IdBusMapCoach_RNo);
        Assert.Null(request.Status);
        Assert.Null(request.Limit);
    }

    #endregion
}
