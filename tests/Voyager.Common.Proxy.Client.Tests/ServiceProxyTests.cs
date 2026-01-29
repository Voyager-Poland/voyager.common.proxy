namespace Voyager.Common.Proxy.Client.Tests;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Voyager.Common.Results;
using Xunit;

public class ServiceProxyTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsProxy()
    {
        var httpClient = new HttpClient { BaseAddress = new Uri("https://api.example.com") };
        var options = new ServiceProxyOptions { BaseUrl = new Uri("https://api.example.com") };

        var proxy = ServiceProxy<ITestService>.Create(httpClient, options);

        proxy.Should().NotBeNull();
        proxy.Should().BeAssignableTo<ITestService>();
    }

    [Fact]
    public void Create_WithNullHttpClient_ThrowsArgumentNullException()
    {
        var options = new ServiceProxyOptions { BaseUrl = new Uri("https://api.example.com") };

        var act = () => ServiceProxy<ITestService>.Create(null!, options);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Create_WithNullOptions_ThrowsArgumentNullException()
    {
        var httpClient = new HttpClient();

        var act = () => ServiceProxy<ITestService>.Create(httpClient, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    #endregion

    #region Proxy Invocation Tests

    [Fact]
    public async Task Proxy_GetMethod_MakesGetRequest()
    {
        var handler = new TestHttpMessageHandler(request =>
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.PathAndQuery.Should().Contain("/test-service/get-user");
            request.RequestUri.PathAndQuery.Should().Contain("id=123");

            return CreateJsonResponse(HttpStatusCode.OK, new User { Id = 123, Name = "John" });
        });

        var proxy = CreateProxy<ITestService>(handler);

        var result = await proxy.GetUserAsync(123);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(123);
        result.Value.Name.Should().Be("John");
    }

    [Fact]
    public async Task Proxy_PostMethod_MakesPostRequestWithBody()
    {
        var handler = new TestHttpMessageHandler(async request =>
        {
            request.Method.Should().Be(HttpMethod.Post);
            request.RequestUri!.PathAndQuery.Should().Contain("/test-service/create-user");

            var body = await request.Content!.ReadAsStringAsync();
            body.Should().Contain("\"name\"");
            body.Should().Contain("\"email\"");

            return CreateJsonResponse(HttpStatusCode.Created, new User { Id = 1, Name = "John" });
        });

        var proxy = CreateProxy<ITestService>(handler);

        var result = await proxy.CreateUserAsync(new CreateUserRequest { Name = "John", Email = "john@test.com" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(1);
    }

    [Fact]
    public async Task Proxy_DeleteMethod_MakesDeleteRequest()
    {
        var handler = new TestHttpMessageHandler(request =>
        {
            request.Method.Should().Be(HttpMethod.Delete);
            request.RequestUri!.PathAndQuery.Should().Contain("/test-service/delete-user");
            request.RequestUri.PathAndQuery.Should().Contain("id=123");

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });

        var proxy = CreateProxy<ITestService>(handler);

        var result = await proxy.DeleteUserAsync(123);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Proxy_UpdateMethod_MakesPutRequest()
    {
        var handler = new TestHttpMessageHandler(request =>
        {
            request.Method.Should().Be(HttpMethod.Put);
            request.RequestUri!.PathAndQuery.Should().Contain("/test-service/update-user");

            return CreateJsonResponse(HttpStatusCode.OK, new User { Id = 1, Name = "Updated" });
        });

        var proxy = CreateProxy<ITestService>(handler);

        var result = await proxy.UpdateUserAsync(new User { Id = 1, Name = "Updated" });

        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Proxy_404Response_ReturnsNotFoundError()
    {
        var handler = TestHttpMessageHandler.FromSync(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("User not found")
            });

        var proxy = CreateProxy<ITestService>(handler);

        var result = await proxy.GetUserAsync(999);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Proxy_500Response_ReturnsUnexpectedError()
    {
        var handler = TestHttpMessageHandler.FromSync(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal server error")
            });

        var proxy = CreateProxy<ITestService>(handler);

        var result = await proxy.GetUserAsync(1);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unexpected);
    }

    [Fact]
    public async Task Proxy_NetworkError_ReturnsUnavailableError()
    {
        var handler = TestHttpMessageHandler.FromSync(_ =>
            throw new HttpRequestException("Connection refused"));

        var proxy = CreateProxy<ITestService>(handler);

        var result = await proxy.GetUserAsync(1);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unavailable);
        result.Error.Message.Should().Contain("Connection");
    }

    [Fact]
    public async Task Proxy_Cancellation_ReturnsCancelledError()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = TestHttpMessageHandler.FromSync(_ =>
            throw new OperationCanceledException());

        var proxy = CreateProxy<ITestService>(handler);

        var result = await proxy.GetUserWithCancellationAsync(1, cts.Token);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Cancelled);
    }

    #endregion

    #region Synchronous Method Tests

    [Fact]
    public void Proxy_SynchronousMethod_ThrowsNotSupportedException()
    {
        var handler = TestHttpMessageHandler.FromSync(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var proxy = CreateProxy<IServiceWithSyncMethod>(handler);

        var act = () => proxy.GetUserSync(1);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Synchronous*");
    }

    #endregion

    #region Non-Result Return Type Tests

    [Fact]
    public async Task Proxy_NonResultReturnType_ThrowsNotSupportedException()
    {
        var handler = TestHttpMessageHandler.FromSync(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var proxy = CreateProxy<IServiceWithWrongReturnType>(handler);

        Func<Task> act = async () => await proxy.GetUserAsync(1);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Result*");
    }

    #endregion

    #region Helper Methods

    private static TService CreateProxy<TService>(TestHttpMessageHandler handler)
        where TService : class
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.com")
        };

        var options = new ServiceProxyOptions
        {
            BaseUrl = new Uri("https://api.example.com")
        };

        return ServiceProxy<TService>.Create(httpClient, options);
    }

    private static HttpResponseMessage CreateJsonResponse<T>(HttpStatusCode statusCode, T content)
    {
        var json = JsonSerializer.Serialize(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    #endregion

    #region Test Classes

    public class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = request => Task.FromResult(handler(request));
        }

        public TestHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public static TestHttpMessageHandler FromSync(Func<HttpRequestMessage, HttpResponseMessage> handler)
            => new(handler);

        public static TestHttpMessageHandler FromAsync(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            => new(handler);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await _handler(request);
        }
    }

    public interface ITestService
    {
        Task<Result<User>> GetUserAsync(int id);
        Task<Result<User>> CreateUserAsync(CreateUserRequest request);
        Task<Result<User>> UpdateUserAsync(User user);
        Task<Result> DeleteUserAsync(int id);
        Task<Result<User>> GetUserWithCancellationAsync(int id, CancellationToken cancellationToken);
    }

    public interface IServiceWithSyncMethod
    {
        Result<User> GetUserSync(int id);
    }

    public interface IServiceWithWrongReturnType
    {
        Task<User> GetUserAsync(int id);
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class CreateUserRequest
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
    }

    #endregion
}
