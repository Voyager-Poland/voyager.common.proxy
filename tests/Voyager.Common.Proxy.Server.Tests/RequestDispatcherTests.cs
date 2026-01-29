using System.Text;
using System.Text.Json;
using System.Threading;
using Voyager.Common.Proxy.Server.Abstractions;
using Voyager.Common.Proxy.Server.Core;
using Voyager.Common.Results;

namespace Voyager.Common.Proxy.Server.Tests;

public class RequestDispatcherTests
{
    private readonly RequestDispatcher _dispatcher = new();

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

    private class TestResponseWriter : IResponseWriter
    {
        public int? StatusCode { get; private set; }
        public object? WrittenValue { get; private set; }
        public string? ErrorType { get; private set; }
        public string? ErrorMessage { get; private set; }
        public bool NoContentWritten { get; private set; }

        public Task WriteJsonAsync<T>(T value, int statusCode)
        {
            StatusCode = statusCode;
            WrittenValue = value;
            return Task.CompletedTask;
        }

        public Task WriteErrorAsync(string errorType, string message)
        {
            ErrorType = errorType;
            ErrorMessage = message;
            return Task.CompletedTask;
        }

        public Task WriteNoContentAsync()
        {
            NoContentWritten = true;
            StatusCode = 204;
            return Task.CompletedTask;
        }
    }

    public interface ITestService
    {
        Task<Result<User>> GetUserAsync(int id);
        Task<Result<User>> GetUserWithTokenAsync(int id, CancellationToken cancellationToken);
        Task<Result> DeleteUserAsync(int id);
        Task<Result<User>> CreateUserAsync(CreateUserRequest request);
        Task<Result<User>> FailingMethodAsync();
        Task<Result<User>> ThrowingMethodAsync();
    }

    public class TestService : ITestService
    {
        public Task<Result<User>> GetUserAsync(int id)
        {
            return Task.FromResult(Result<User>.Success(new User { Id = id, Name = "Test User" }));
        }

        public Task<Result<User>> GetUserWithTokenAsync(int id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Result<User>.Success(new User { Id = id, Name = "Test User" }));
        }

        public Task<Result> DeleteUserAsync(int id)
        {
            return Task.FromResult(Result.Success());
        }

        public Task<Result<User>> CreateUserAsync(CreateUserRequest request)
        {
            return Task.FromResult(Result<User>.Success(new User { Id = 1, Name = request.Name }));
        }

        public Task<Result<User>> FailingMethodAsync()
        {
            return Task.FromResult(Result<User>.Failure(Error.NotFoundError("User not found")));
        }

        public Task<Result<User>> ThrowingMethodAsync()
        {
            throw new InvalidOperationException("Something went wrong");
        }
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

    private static EndpointDescriptor CreateEndpoint<TService>(string methodName)
    {
        var method = typeof(TService).GetMethod(methodName)!;
        var parameters = new List<ParameterDescriptor>();

        foreach (var param in method.GetParameters())
        {
            ParameterSource source;
            if (param.ParameterType == typeof(CancellationToken))
            {
                source = ParameterSource.CancellationToken;
            }
            else if (param.ParameterType.IsClass && param.ParameterType != typeof(string))
            {
                source = ParameterSource.Body;
            }
            else
            {
                source = ParameterSource.Route;
            }

            parameters.Add(new ParameterDescriptor(
                param.Name!,
                param.ParameterType,
                source,
                param.HasDefaultValue,
                param.DefaultValue));
        }

        // Determine result type
        var returnType = method.ReturnType;
        Type? resultType = null;
        Type? resultValueType = null;

        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            var innerType = returnType.GetGenericArguments()[0];
            resultType = innerType;

            if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                resultValueType = innerType.GetGenericArguments()[0];
            }
        }

        return new EndpointDescriptor(
            typeof(TService),
            method,
            "GET",
            "/test/{id}",
            parameters,
            resultType!,
            resultValueType);
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

    #region Success Response Tests

    [Fact]
    public async Task Dispatch_WithSuccessResult_WritesJsonWith200()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["id"] = "42" }
        };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<ITestService>("GetUserAsync");
        var service = new TestService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal(200, responseWriter.StatusCode);
        Assert.NotNull(responseWriter.WrittenValue);
        var user = Assert.IsType<User>(responseWriter.WrittenValue);
        Assert.Equal(42, user.Id);
    }

    [Fact]
    public async Task Dispatch_WithVoidResult_WritesNoContent()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["id"] = "42" }
        };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<ITestService>("DeleteUserAsync");
        var service = new TestService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.True(responseWriter.NoContentWritten);
    }

    [Fact]
    public async Task Dispatch_WithCancellationToken_PassesToken()
    {
        var cts = new CancellationTokenSource();
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["id"] = "42" },
            CancellationToken = cts.Token
        };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<ITestService>("GetUserWithTokenAsync");
        var service = new TestService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal(200, responseWriter.StatusCode);
    }

    [Fact]
    public async Task Dispatch_WithBodyParameter_DeserializesRequest()
    {
        var request = new CreateUserRequest { Name = "John" };
        var context = new TestRequestContext
        {
            Body = CreateJsonBody(request)
        };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<ITestService>("CreateUserAsync");
        var service = new TestService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal(200, responseWriter.StatusCode);
        var user = Assert.IsType<User>(responseWriter.WrittenValue);
        Assert.Equal("John", user.Name);
    }

    #endregion

    #region Error Response Tests

    [Fact]
    public async Task Dispatch_WithFailedResult_WritesError()
    {
        var context = new TestRequestContext();
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<ITestService>("FailingMethodAsync");
        var service = new TestService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.NotNull(responseWriter.ErrorType);
        Assert.NotNull(responseWriter.ErrorMessage);
        Assert.Contains("not found", responseWriter.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_WithException_WritesErrorResponse()
    {
        var context = new TestRequestContext();
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<ITestService>("ThrowingMethodAsync");
        var service = new TestService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.NotNull(responseWriter.ErrorType);
        Assert.NotNull(responseWriter.ErrorMessage);
    }

    [Fact]
    public async Task Dispatch_WithInvalidRouteParameter_WritesErrorResponse()
    {
        var context = new TestRequestContext
        {
            RouteValues = new Dictionary<string, string> { ["id"] = "not-a-number" }
        };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<ITestService>("GetUserAsync");
        var service = new TestService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.NotNull(responseWriter.ErrorType);
        Assert.NotNull(responseWriter.ErrorMessage);
    }

    #endregion
}
