namespace Voyager.Common.Proxy.Client.Tests;

using System.Net;
using FluentAssertions;
using Voyager.Common.Proxy.Abstractions.Validation;
using Voyager.Common.Results;
using Xunit;

public class ClientValidationTests
{
    #region Test Models - Interface approach

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

    public class ValidatableBoolRequest : IValidatableRequestBool
    {
        public int Id { get; set; }

        public bool IsValid() => Id > 0;

        public string? ValidationErrorMessage => Id <= 0 ? "Id must be positive" : null;
    }

    public class RegularRequest
    {
        public string Name { get; set; } = "";
    }

    #endregion

    #region Test Models - Attribute approach

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

    public class AttributeBoolValidatedRequest
    {
        public int Id { get; set; }

        [ValidationMethod(ErrorMessage = "Id must be greater than zero")]
        public bool CheckValid() => Id > 0;
    }

    #endregion

    #region Test Services

    // Service with ClientSide = true (validation on client)
    [ValidateRequest(ClientSide = true)]
    public interface IClientValidatedService
    {
        Task<Result<string>> ProcessValidatableAsync(ValidatableRequest request);
        Task<Result<string>> ProcessValidatableBoolAsync(ValidatableBoolRequest request);
        Task<Result<string>> ProcessAttributeValidatedAsync(AttributeValidatedRequest request);
        Task<Result<string>> ProcessAttributeBoolValidatedAsync(AttributeBoolValidatedRequest request);
        Task<Result<string>> ProcessRegularAsync(RegularRequest request);
    }

    // Service with ClientSide = false (no client validation)
    [ValidateRequest(ClientSide = false)]
    public interface IServerOnlyValidatedService
    {
        Task<Result<string>> ProcessValidatableAsync(ValidatableRequest request);
    }

    // Service without ValidateRequest attribute
    public interface IUnvalidatedService
    {
        Task<Result<string>> ProcessValidatableAsync(ValidatableRequest request);
    }

    // Service with method-level validation override
    public interface IMixedValidationService
    {
        [ValidateRequest(ClientSide = true)]
        Task<Result<string>> ProcessWithClientValidationAsync(ValidatableRequest request);

        [ValidateRequest(ClientSide = false)]
        Task<Result<string>> ProcessWithServerOnlyValidationAsync(ValidatableRequest request);
    }

    #endregion

    #region IValidatableRequest Client Tests

    [Fact]
    public async Task ClientValidation_WithValidIValidatableRequest_MakesRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new ValidatableRequest { Amount = 100, Currency = "USD" };

        var result = await proxy.ProcessValidatableAsync(request);

        result.IsSuccess.Should().BeTrue();
        requestMade.Should().BeTrue();
    }

    [Fact]
    public async Task ClientValidation_WithInvalidIValidatableRequest_ReturnsErrorWithoutRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new ValidatableRequest { Amount = -10, Currency = "USD" };

        var result = await proxy.ProcessValidatableAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Amount must be positive");
        requestMade.Should().BeFalse("validation should prevent HTTP request");
    }

    [Fact]
    public async Task ClientValidation_WithMissingCurrency_ReturnsErrorWithoutRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new ValidatableRequest { Amount = 100, Currency = null };

        var result = await proxy.ProcessValidatableAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Currency is required");
        requestMade.Should().BeFalse();
    }

    #endregion

    #region IValidatableRequestBool Client Tests

    [Fact]
    public async Task ClientValidation_WithValidIValidatableRequestBool_MakesRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new ValidatableBoolRequest { Id = 42 };

        var result = await proxy.ProcessValidatableBoolAsync(request);

        result.IsSuccess.Should().BeTrue();
        requestMade.Should().BeTrue();
    }

    [Fact]
    public async Task ClientValidation_WithInvalidIValidatableRequestBool_ReturnsErrorWithoutRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new ValidatableBoolRequest { Id = -1 };

        var result = await proxy.ProcessValidatableBoolAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Id must be positive");
        requestMade.Should().BeFalse();
    }

    #endregion

    #region ValidationMethod Attribute Client Tests

    [Fact]
    public async Task ClientValidation_WithValidAttributeValidatedRequest_MakesRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new AttributeValidatedRequest { Amount = 100, Currency = "EUR" };

        var result = await proxy.ProcessAttributeValidatedAsync(request);

        result.IsSuccess.Should().BeTrue();
        requestMade.Should().BeTrue();
    }

    [Fact]
    public async Task ClientValidation_WithInvalidAttributeValidatedRequest_ReturnsErrorWithoutRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new AttributeValidatedRequest { Amount = -50, Currency = "EUR" };

        var result = await proxy.ProcessAttributeValidatedAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Amount must be positive");
        requestMade.Should().BeFalse();
    }

    [Fact]
    public async Task ClientValidation_WithValidAttributeBoolValidatedRequest_MakesRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new AttributeBoolValidatedRequest { Id = 10 };

        var result = await proxy.ProcessAttributeBoolValidatedAsync(request);

        result.IsSuccess.Should().BeTrue();
        requestMade.Should().BeTrue();
    }

    [Fact]
    public async Task ClientValidation_WithInvalidAttributeBoolValidatedRequest_ReturnsErrorWithoutRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new AttributeBoolValidatedRequest { Id = 0 };

        var result = await proxy.ProcessAttributeBoolValidatedAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Id must be greater than zero");
        requestMade.Should().BeFalse();
    }

    #endregion

    #region No Client Validation Tests

    [Fact]
    public async Task NoClientValidation_WithServerOnlyAttribute_MakesRequestEvenIfInvalid()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IServerOnlyValidatedService>(handler);
        var request = new ValidatableRequest { Amount = -100, Currency = null };

        var result = await proxy.ProcessValidatableAsync(request);

        // Server-only validation means client does NOT validate
        // HTTP request should be made (server will validate)
        result.IsSuccess.Should().BeTrue();
        requestMade.Should().BeTrue();
    }

    [Fact]
    public async Task NoClientValidation_WithoutAttribute_MakesRequestEvenIfInvalid()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IUnvalidatedService>(handler);
        var request = new ValidatableRequest { Amount = -100, Currency = null };

        var result = await proxy.ProcessValidatableAsync(request);

        // No validation attribute means no client-side validation
        result.IsSuccess.Should().BeTrue();
        requestMade.Should().BeTrue();
    }

    [Fact]
    public async Task NoClientValidation_WithRegularRequest_MakesRequest()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IClientValidatedService>(handler);
        var request = new RegularRequest { Name = "Test" };

        var result = await proxy.ProcessRegularAsync(request);

        // Regular request without validation interface works fine
        result.IsSuccess.Should().BeTrue();
        requestMade.Should().BeTrue();
    }

    #endregion

    #region Method-Level Override Tests

    [Fact]
    public async Task MethodLevelValidation_WithClientSideTrue_ValidatesOnClient()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IMixedValidationService>(handler);
        var request = new ValidatableRequest { Amount = -10, Currency = "USD" };

        var result = await proxy.ProcessWithClientValidationAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        requestMade.Should().BeFalse();
    }

    [Fact]
    public async Task MethodLevelValidation_WithClientSideFalse_DoesNotValidateOnClient()
    {
        var requestMade = false;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestMade = true;
            return CreateSuccessResponse("Processed");
        });

        var proxy = CreateProxy<IMixedValidationService>(handler);
        var request = new ValidatableRequest { Amount = -10, Currency = "USD" };

        var result = await proxy.ProcessWithServerOnlyValidationAsync(request);

        // ClientSide = false means no client validation
        result.IsSuccess.Should().BeTrue();
        requestMade.Should().BeTrue();
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

    private static HttpResponseMessage CreateSuccessResponse(string content)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(content);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }

    #endregion

    #region Test Helpers

    public class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_handler(request));
        }
    }

    #endregion
}
