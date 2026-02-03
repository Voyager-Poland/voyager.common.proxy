using System.Text;
using System.Text.Json;
using System.Threading;
using Voyager.Common.Proxy.Abstractions.Validation;
using Voyager.Common.Proxy.Server.Abstractions;
using Voyager.Common.Proxy.Server.Core;
using Voyager.Common.Results;

namespace Voyager.Common.Proxy.Server.Tests;

public class RequestValidationTests
{
    private readonly RequestDispatcher _dispatcher = new();

    #region Test Helpers

    private class TestRequestContext : IRequestContext
    {
        public string HttpMethod { get; set; } = "POST";
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
            StatusCode = 400;
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

    #endregion

    #region Test Models - Interface approach

    // Request implementing IValidatableRequest
    public class ValidatableRequest : IValidatableRequest
    {
        public decimal Amount { get; set; }
        public string? Currency { get; set; }

        public Result IsValid()
        {
            if (Amount <= 0)
                return Result.Failure(Error.ValidationError("Amount must be positive"));
            if (string.IsNullOrEmpty(Currency))
                return Result.Failure(Error.ValidationError("Currency is required"));
            return Result.Success();
        }
    }

    // Request implementing IValidatableRequestBool
    public class ValidatableBoolRequest : IValidatableRequestBool
    {
        public int Id { get; set; }

        public bool IsValid() => Id > 0;

        public string? ValidationErrorMessage => Id <= 0 ? "Id must be positive" : null;
    }

    // Regular request without validation
    public class RegularRequest
    {
        public string Name { get; set; } = "";
    }

    #endregion

    #region Test Models - Attribute approach

    // Request with [ValidationMethod] returning Result
    public class AttributeValidatedRequest
    {
        public decimal Amount { get; set; }
        public string? Currency { get; set; }

        [ValidationMethod]
        public Result Validate()
        {
            if (Amount <= 0)
                return Result.Failure(Error.ValidationError("Amount must be positive"));
            if (string.IsNullOrEmpty(Currency))
                return Result.Failure(Error.ValidationError("Currency is required"));
            return Result.Success();
        }
    }

    // Request with [ValidationMethod] returning bool
    public class AttributeBoolValidatedRequest
    {
        public int Id { get; set; }

        [ValidationMethod(ErrorMessage = "Id must be greater than zero")]
        public bool CheckValid() => Id > 0;
    }

    #endregion

    #region Test Services

    [ValidateRequest]
    public interface IValidatedService
    {
        Task<Result<string>> ProcessValidatableAsync(ValidatableRequest request);
        Task<Result<string>> ProcessValidatableBoolAsync(ValidatableBoolRequest request);
        Task<Result<string>> ProcessAttributeValidatedAsync(AttributeValidatedRequest request);
        Task<Result<string>> ProcessAttributeBoolValidatedAsync(AttributeBoolValidatedRequest request);
        Task<Result<string>> ProcessRegularAsync(RegularRequest request);
    }

    public interface IUnvalidatedService
    {
        Task<Result<string>> ProcessValidatableAsync(ValidatableRequest request);
    }

    public class ValidatedService : IValidatedService
    {
        public Task<Result<string>> ProcessValidatableAsync(ValidatableRequest request)
            => Task.FromResult(Result<string>.Success($"Processed: {request.Amount} {request.Currency}"));

        public Task<Result<string>> ProcessValidatableBoolAsync(ValidatableBoolRequest request)
            => Task.FromResult(Result<string>.Success($"Processed: {request.Id}"));

        public Task<Result<string>> ProcessAttributeValidatedAsync(AttributeValidatedRequest request)
            => Task.FromResult(Result<string>.Success($"Processed: {request.Amount} {request.Currency}"));

        public Task<Result<string>> ProcessAttributeBoolValidatedAsync(AttributeBoolValidatedRequest request)
            => Task.FromResult(Result<string>.Success($"Processed: {request.Id}"));

        public Task<Result<string>> ProcessRegularAsync(RegularRequest request)
            => Task.FromResult(Result<string>.Success($"Processed: {request.Name}"));
    }

    public class UnvalidatedService : IUnvalidatedService
    {
        public Task<Result<string>> ProcessValidatableAsync(ValidatableRequest request)
            => Task.FromResult(Result<string>.Success($"Processed: {request.Amount} {request.Currency}"));
    }

    #endregion

    #region Endpoint Helpers

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
            "POST",
            "/test",
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

    #region IValidatableRequest Tests

    [Fact]
    public async Task Dispatch_WithValidIValidatableRequest_Succeeds()
    {
        var request = new ValidatableRequest { Amount = 100, Currency = "USD" };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessValidatableAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal(200, responseWriter.StatusCode);
        Assert.Null(responseWriter.ErrorType);
    }

    [Fact]
    public async Task Dispatch_WithInvalidIValidatableRequest_ReturnsValidationError()
    {
        var request = new ValidatableRequest { Amount = -10, Currency = "USD" };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessValidatableAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal("Validation", responseWriter.ErrorType);
        Assert.Contains("Amount must be positive", responseWriter.ErrorMessage);
    }

    [Fact]
    public async Task Dispatch_WithMissingCurrency_ReturnsValidationError()
    {
        var request = new ValidatableRequest { Amount = 100, Currency = null };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessValidatableAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal("Validation", responseWriter.ErrorType);
        Assert.Contains("Currency is required", responseWriter.ErrorMessage);
    }

    #endregion

    #region IValidatableRequestBool Tests

    [Fact]
    public async Task Dispatch_WithValidIValidatableRequestBool_Succeeds()
    {
        var request = new ValidatableBoolRequest { Id = 42 };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessValidatableBoolAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal(200, responseWriter.StatusCode);
        Assert.Null(responseWriter.ErrorType);
    }

    [Fact]
    public async Task Dispatch_WithInvalidIValidatableRequestBool_ReturnsValidationError()
    {
        var request = new ValidatableBoolRequest { Id = -1 };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessValidatableBoolAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal("Validation", responseWriter.ErrorType);
        Assert.Contains("Id must be positive", responseWriter.ErrorMessage);
    }

    #endregion

    #region ValidationMethod Attribute Tests

    [Fact]
    public async Task Dispatch_WithValidAttributeValidatedRequest_Succeeds()
    {
        var request = new AttributeValidatedRequest { Amount = 100, Currency = "EUR" };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessAttributeValidatedAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal(200, responseWriter.StatusCode);
        Assert.Null(responseWriter.ErrorType);
    }

    [Fact]
    public async Task Dispatch_WithInvalidAttributeValidatedRequest_ReturnsValidationError()
    {
        var request = new AttributeValidatedRequest { Amount = -50, Currency = "EUR" };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessAttributeValidatedAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal("Validation", responseWriter.ErrorType);
        Assert.Contains("Amount must be positive", responseWriter.ErrorMessage);
    }

    [Fact]
    public async Task Dispatch_WithValidAttributeBoolValidatedRequest_Succeeds()
    {
        var request = new AttributeBoolValidatedRequest { Id = 10 };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessAttributeBoolValidatedAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal(200, responseWriter.StatusCode);
        Assert.Null(responseWriter.ErrorType);
    }

    [Fact]
    public async Task Dispatch_WithInvalidAttributeBoolValidatedRequest_ReturnsValidationError()
    {
        var request = new AttributeBoolValidatedRequest { Id = 0 };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessAttributeBoolValidatedAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal("Validation", responseWriter.ErrorType);
        Assert.Contains("Id must be greater than zero", responseWriter.ErrorMessage);
    }

    #endregion

    #region No Validation Attribute Tests

    [Fact]
    public async Task Dispatch_WithoutValidateRequestAttribute_SkipsValidation()
    {
        // Even with invalid data, validation is skipped because interface doesn't have [ValidateRequest]
        var request = new ValidatableRequest { Amount = -100, Currency = null };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IUnvalidatedService>("ProcessValidatableAsync");
        var service = new UnvalidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        // Should succeed because validation is not enabled
        Assert.Equal(200, responseWriter.StatusCode);
        Assert.Null(responseWriter.ErrorType);
    }

    [Fact]
    public async Task Dispatch_WithRegularRequest_Succeeds()
    {
        // Regular request without validation interface should work
        var request = new RegularRequest { Name = "Test" };
        var context = new TestRequestContext { Body = CreateJsonBody(request) };
        var responseWriter = new TestResponseWriter();
        var endpoint = CreateEndpoint<IValidatedService>("ProcessRegularAsync");
        var service = new ValidatedService();

        await _dispatcher.DispatchAsync(context, responseWriter, endpoint, service);

        Assert.Equal(200, responseWriter.StatusCode);
        Assert.Null(responseWriter.ErrorType);
    }

    #endregion
}
