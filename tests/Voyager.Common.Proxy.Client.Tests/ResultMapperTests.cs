namespace Voyager.Common.Proxy.Client.Tests;

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Voyager.Common.Proxy.Client.Internal;
using Voyager.Common.Results;
using Xunit;

public class ResultMapperTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    #region Success Mapping Tests

    [Fact]
    public async Task MapResponseAsync_200WithBody_ReturnsSuccessWithValue()
    {
        var user = new User { Id = 1, Name = "John" };
        var response = CreateResponse(HttpStatusCode.OK, user);

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsSuccess.Should().BeTrue();
        typedResult.Value.Should().NotBeNull();
        typedResult.Value!.Id.Should().Be(1);
        typedResult.Value.Name.Should().Be("John");
    }

    [Fact]
    public async Task MapResponseAsync_201Created_ReturnsSuccessWithValue()
    {
        var user = new User { Id = 1, Name = "John" };
        var response = CreateResponse(HttpStatusCode.Created, user);

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsSuccess.Should().BeTrue();
        typedResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task MapResponseAsync_204NoContent_ReturnsSuccessWithDefault()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NoContent);

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsSuccess.Should().BeTrue();
        typedResult.Value.Should().BeNull();
    }

    [Fact]
    public async Task MapResponseAsync_200NonGenericResult_ReturnsSuccess()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK);

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result), JsonOptions);

        result.Should().BeOfType<Result>();
        var typedResult = (Result)result;
        typedResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MapResponseAsync_200EmptyBody_ReturnsSuccessWithDefault()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("")
        };

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Error Mapping Tests

    [Fact]
    public async Task MapResponseAsync_400BadRequest_ReturnsValidationError()
    {
        var response = CreateErrorResponse(HttpStatusCode.BadRequest, "Invalid input");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Validation);
        typedResult.Error.Message.Should().Contain("Invalid input");
    }

    [Fact]
    public async Task MapResponseAsync_401Unauthorized_ReturnsUnauthorizedError()
    {
        var response = CreateErrorResponse(HttpStatusCode.Unauthorized, "Not authenticated");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task MapResponseAsync_403Forbidden_ReturnsPermissionError()
    {
        var response = CreateErrorResponse(HttpStatusCode.Forbidden, "Access denied");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Permission);
    }

    [Fact]
    public async Task MapResponseAsync_404NotFound_ReturnsNotFoundError()
    {
        var response = CreateErrorResponse(HttpStatusCode.NotFound, "User not found");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.NotFound);
        typedResult.Error.Message.Should().Contain("User not found");
    }

    [Fact]
    public async Task MapResponseAsync_409Conflict_ReturnsConflictError()
    {
        var response = CreateErrorResponse(HttpStatusCode.Conflict, "Already exists");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task MapResponseAsync_408RequestTimeout_ReturnsTimeoutError()
    {
        var response = CreateErrorResponse(HttpStatusCode.RequestTimeout, "Timeout");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Timeout);
    }

    [Fact]
    public async Task MapResponseAsync_429TooManyRequests_ReturnsTooManyRequestsError()
    {
        var response = CreateErrorResponse((HttpStatusCode)429, "Rate limited");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.TooManyRequests);
    }

    [Fact]
    public async Task MapResponseAsync_502BadGateway_ReturnsUnavailableError()
    {
        var response = CreateErrorResponse(HttpStatusCode.BadGateway, "Bad gateway");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Unavailable);
    }

    [Fact]
    public async Task MapResponseAsync_503ServiceUnavailable_ReturnsUnavailableError()
    {
        var response = CreateErrorResponse(HttpStatusCode.ServiceUnavailable, "Service down");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Unavailable);
    }

    [Fact]
    public async Task MapResponseAsync_500InternalServerError_ReturnsUnexpectedError()
    {
        var response = CreateErrorResponse(HttpStatusCode.InternalServerError, "Server error");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Unexpected);
    }

    [Fact]
    public async Task MapResponseAsync_504GatewayTimeout_ReturnsTimeoutError()
    {
        var response = CreateErrorResponse(HttpStatusCode.GatewayTimeout, "Gateway timeout");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        result.Should().BeOfType<Result<User>>();
        var typedResult = (Result<User>)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Timeout);
    }

    [Fact]
    public async Task MapResponseAsync_NonGenericResult_400_ReturnsFailure()
    {
        var response = CreateErrorResponse(HttpStatusCode.BadRequest, "Bad request");

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result), JsonOptions);

        result.Should().BeOfType<Result>();
        var typedResult = (Result)result;
        typedResult.IsFailure.Should().BeTrue();
        typedResult.Error!.Type.Should().Be(ErrorType.Validation);
    }

    #endregion

    #region Error Message Parsing Tests

    [Fact]
    public async Task MapResponseAsync_JsonWithErrorField_ExtractsMessage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Custom error message\"}")
        };

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        var typedResult = (Result<User>)result;
        typedResult.Error!.Message.Should().Be("Custom error message");
    }

    [Fact]
    public async Task MapResponseAsync_JsonWithMessageField_ExtractsMessage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"message\":\"Another error\"}")
        };

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        var typedResult = (Result<User>)result;
        typedResult.Error!.Message.Should().Be("Another error");
    }

    [Fact]
    public async Task MapResponseAsync_JsonWithTitleField_ExtractsMessage()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"title\":\"Validation failed\"}")
        };

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        var typedResult = (Result<User>)result;
        typedResult.Error!.Message.Should().Be("Validation failed");
    }

    [Fact]
    public async Task MapResponseAsync_PlainTextError_UsesContent()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Plain text error")
        };

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        var typedResult = (Result<User>)result;
        typedResult.Error!.Message.Should().Be("Plain text error");
    }

    [Fact]
    public async Task MapResponseAsync_EmptyErrorBody_UsesReasonPhrase()
    {
        var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            ReasonPhrase = "Not Found",
            Content = new StringContent("")
        };

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<User>), JsonOptions);

        var typedResult = (Result<User>)result;
        typedResult.Error!.Message.Should().Be("Not Found");
    }

    #endregion

    #region Value Type Tests

    [Fact]
    public async Task MapResponseAsync_ValueType_ReturnsCorrectValue()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("42")
        };

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<int>), JsonOptions);

        result.Should().BeOfType<Result<int>>();
        var typedResult = (Result<int>)result;
        typedResult.IsSuccess.Should().BeTrue();
        typedResult.Value.Should().Be(42);
    }

    [Fact]
    public async Task MapResponseAsync_StringType_ReturnsCorrectValue()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("\"hello world\"")
        };

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<string>), JsonOptions);

        result.Should().BeOfType<Result<string>>();
        var typedResult = (Result<string>)result;
        typedResult.IsSuccess.Should().BeTrue();
        typedResult.Value.Should().Be("hello world");
    }

    [Fact]
    public async Task MapResponseAsync_ListType_ReturnsCorrectValue()
    {
        var users = new List<User>
        {
            new() { Id = 1, Name = "John" },
            new() { Id = 2, Name = "Jane" }
        };
        var response = CreateResponse(HttpStatusCode.OK, users);

        var result = await ResultMapper.MapResponseAsync(response, typeof(Result<List<User>>), JsonOptions);

        result.Should().BeOfType<Result<List<User>>>();
        var typedResult = (Result<List<User>>)result;
        typedResult.IsSuccess.Should().BeTrue();
        typedResult.Value.Should().HaveCount(2);
    }

    #endregion

    #region Helper Methods

    private static HttpResponseMessage CreateResponse<T>(HttpStatusCode statusCode, T content)
    {
        var json = JsonSerializer.Serialize(content, JsonOptions);
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage CreateErrorResponse(HttpStatusCode statusCode, string message)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(message)
        };
    }

    #endregion

    #region Test Classes

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion
}
