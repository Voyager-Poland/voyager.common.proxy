namespace Voyager.Common.Proxy.Client.Tests;

using FluentAssertions;
using Voyager.Common.Proxy.Client.Extensions;
using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;
using Xunit;

public class ResultResilienceExtensionsTests
{
    #region IsTransient Tests (using Voyager.Common.Results.Extensions)

    [Theory]
    [InlineData(ErrorType.Unavailable)]
    [InlineData(ErrorType.Timeout)]
    [InlineData(ErrorType.TooManyRequests)]
    [InlineData(ErrorType.CircuitBreakerOpen)]
    public void IsTransient_TransientErrorTypes_ReturnsTrue(ErrorType errorType)
    {
        var error = CreateError(errorType);

        error.Type.IsTransient().Should().BeTrue();
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Permission)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Business)]
    [InlineData(ErrorType.Cancelled)]
    [InlineData(ErrorType.Database)]
    [InlineData(ErrorType.Unexpected)]
    public void IsTransient_NonTransientErrorTypes_ReturnsFalse(ErrorType errorType)
    {
        var error = CreateError(errorType);

        error.Type.IsTransient().Should().BeFalse();
    }

    #endregion

    #region ShouldCountForCircuitBreaker Tests (using Voyager.Common.Results.Extensions)

    [Theory]
    [InlineData(ErrorType.Unavailable)]
    [InlineData(ErrorType.Timeout)]
    [InlineData(ErrorType.TooManyRequests)]
    [InlineData(ErrorType.Database)]
    [InlineData(ErrorType.Unexpected)]
    public void ShouldCountForCircuitBreaker_InfrastructureErrorTypes_ReturnsTrue(ErrorType errorType)
    {
        var error = CreateError(errorType);

        error.Type.ShouldCountForCircuitBreaker().Should().BeTrue();
    }

    [Theory]
    [InlineData(ErrorType.Validation)]
    [InlineData(ErrorType.NotFound)]
    [InlineData(ErrorType.Permission)]
    [InlineData(ErrorType.Unauthorized)]
    [InlineData(ErrorType.Conflict)]
    [InlineData(ErrorType.Business)]
    [InlineData(ErrorType.Cancelled)]
    [InlineData(ErrorType.CircuitBreakerOpen)] // CB doesn't count itself
    public void ShouldCountForCircuitBreaker_BusinessErrorTypes_ReturnsFalse(ErrorType errorType)
    {
        var error = CreateError(errorType);

        error.Type.ShouldCountForCircuitBreaker().Should().BeFalse();
    }

    #endregion

    #region TransientErrorPolicy Tests

    [Fact]
    public void TransientErrorPolicy_FirstAttemptWithTransientError_ReturnsContinueWithDelay()
    {
        var policy = ResultResilienceExtensions.TransientErrorPolicy();
        var error = Error.UnavailableError("Service down");

        var result = policy(1, error);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1000); // base delay
    }

    [Fact]
    public void TransientErrorPolicy_SecondAttemptWithTransientError_ReturnsDoubleDelay()
    {
        var policy = ResultResilienceExtensions.TransientErrorPolicy();
        var error = Error.TimeoutError("Timeout");

        var result = policy(2, error);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2000); // 1000 * 2^1
    }

    [Fact]
    public void TransientErrorPolicy_ThirdAttempt_ReturnsFailure()
    {
        var policy = ResultResilienceExtensions.TransientErrorPolicy(maxAttempts: 3);
        var error = Error.UnavailableError("Service down");

        var result = policy(3, error);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void TransientErrorPolicy_NonTransientError_ReturnsFailureImmediately()
    {
        var policy = ResultResilienceExtensions.TransientErrorPolicy();
        var error = Error.ValidationError("Invalid input");

        var result = policy(1, error);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void TransientErrorPolicy_CustomMaxAttempts_RespectsLimit()
    {
        var policy = ResultResilienceExtensions.TransientErrorPolicy(maxAttempts: 5);
        var error = Error.UnavailableError("Service down");

        // Attempts 1-4 should succeed
        for (int i = 1; i < 5; i++)
        {
            policy(i, error).IsSuccess.Should().BeTrue($"Attempt {i} should continue");
        }

        // Attempt 5 should fail (reached max)
        policy(5, error).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void TransientErrorPolicy_CustomBaseDelay_UsesCorrectDelay()
    {
        var policy = ResultResilienceExtensions.TransientErrorPolicy(baseDelayMs: 500);
        var error = Error.UnavailableError("Service down");

        var result = policy(1, error);

        result.Value.Should().Be(500);
    }

    [Fact]
    public void TransientErrorPolicy_ExponentialBackoff_CalculatesCorrectly()
    {
        var policy = ResultResilienceExtensions.TransientErrorPolicy(maxAttempts: 5, baseDelayMs: 1000);
        var error = Error.UnavailableError("Service down");

        policy(1, error).Value.Should().Be(1000);  // 1000 * 2^0
        policy(2, error).Value.Should().Be(2000);  // 1000 * 2^1
        policy(3, error).Value.Should().Be(4000);  // 1000 * 2^2
        policy(4, error).Value.Should().Be(8000);  // 1000 * 2^3
    }

    #endregion

    #region CustomRetryPolicy Tests

    [Fact]
    public void CustomRetryPolicy_MatchingError_ReturnsDelay()
    {
        var policy = ResultResilienceExtensions.CustomRetryPolicy(
            maxAttempts: 3,
            shouldRetry: e => e.Code == "RATE_LIMIT",
            delayStrategy: attempt => 100 * attempt);

        var error = Error.UnavailableError("RATE_LIMIT", "Rate limited");

        var result = policy(1, error);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(100);
    }

    [Fact]
    public void CustomRetryPolicy_NonMatchingError_ReturnsFailure()
    {
        var policy = ResultResilienceExtensions.CustomRetryPolicy(
            maxAttempts: 3,
            shouldRetry: e => e.Code == "RATE_LIMIT",
            delayStrategy: attempt => 100 * attempt);

        var error = Error.ValidationError("Invalid input");

        var result = policy(1, error);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CustomRetryPolicy_LinearBackoff_CalculatesCorrectly()
    {
        var policy = ResultResilienceExtensions.CustomRetryPolicy(
            maxAttempts: 5,
            shouldRetry: e => e.Type == ErrorType.Unavailable,
            delayStrategy: attempt => 500 * attempt);

        var error = Error.UnavailableError("Service down");

        policy(1, error).Value.Should().Be(500);
        policy(2, error).Value.Should().Be(1000);
        policy(3, error).Value.Should().Be(1500);
    }

    #endregion

    #region RetryAsync Tests

    [Fact]
    public async Task RetryAsync_SuccessOnFirstAttempt_ReturnsImmediately()
    {
        int callCount = 0;
        Func<Task<Result<int>>> operation = () =>
        {
            callCount++;
            return Task.FromResult(Result<int>.Success(42));
        };

        var result = await ResultResilienceExtensions.RetryAsync(operation);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_NonTransientError_DoesNotRetry()
    {
        int callCount = 0;
        Func<Task<Result<int>>> operation = () =>
        {
            callCount++;
            return Task.FromResult(Result<int>.Failure(Error.ValidationError("Invalid")));
        };

        var result = await ResultResilienceExtensions.RetryAsync(operation);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task RetryAsync_TransientErrorThenSuccess_RetriesAndSucceeds()
    {
        int callCount = 0;
        Func<Task<Result<int>>> operation = () =>
        {
            callCount++;
            if (callCount < 3)
                return Task.FromResult(Result<int>.Failure(Error.UnavailableError("Down")));
            return Task.FromResult(Result<int>.Success(42));
        };

        // Use short delay for tests
        var policy = ResultResilienceExtensions.TransientErrorPolicy(maxAttempts: 5, baseDelayMs: 1);
        var result = await ResultResilienceExtensions.RetryAsync(operation, policy);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_AllAttemptsFail_ReturnsOriginalError()
    {
        int callCount = 0;
        Func<Task<Result<int>>> operation = () =>
        {
            callCount++;
            return Task.FromResult(Result<int>.Failure(Error.UnavailableError($"Down attempt {callCount}")));
        };

        var policy = ResultResilienceExtensions.TransientErrorPolicy(maxAttempts: 3, baseDelayMs: 1);
        var result = await ResultResilienceExtensions.RetryAsync(operation, policy);

        result.IsFailure.Should().BeTrue();
        callCount.Should().Be(3);
    }

    [Fact]
    public async Task RetryAsync_TimeoutError_Retries()
    {
        int callCount = 0;
        Func<Task<Result<int>>> operation = () =>
        {
            callCount++;
            if (callCount < 2)
                return Task.FromResult(Result<int>.Failure(Error.TimeoutError("Timeout")));
            return Task.FromResult(Result<int>.Success(42));
        };

        var policy = ResultResilienceExtensions.TransientErrorPolicy(maxAttempts: 3, baseDelayMs: 1);
        var result = await ResultResilienceExtensions.RetryAsync(operation, policy);

        result.IsSuccess.Should().BeTrue();
        callCount.Should().Be(2);
    }

    #endregion

    #region WithRetryAsync Extension Tests

    [Fact]
    public async Task WithRetryAsync_SuccessfulTask_ReturnsSuccess()
    {
        var task = Task.FromResult(Result<int>.Success(42));

        var result = await task.WithRetryAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task WithRetryAsync_FailedTask_ReturnsFailure()
    {
        var task = Task.FromResult(Result<int>.Failure(Error.ValidationError("Invalid")));

        var result = await task.WithRetryAsync();

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    #endregion

    #region Helper Methods

    private static Error CreateError(ErrorType errorType)
    {
        return errorType switch
        {
            ErrorType.Validation => Error.ValidationError("Test"),
            ErrorType.NotFound => Error.NotFoundError("Test"),
            ErrorType.Permission => Error.PermissionError("Test"),
            ErrorType.Unauthorized => Error.UnauthorizedError("Test"),
            ErrorType.Conflict => Error.ConflictError("Test"),
            ErrorType.Business => Error.BusinessError("Test"),
            ErrorType.Cancelled => Error.CancelledError("Test"),
            ErrorType.Database => Error.DatabaseError("Test"),
            ErrorType.Timeout => Error.TimeoutError("Test"),
            ErrorType.Unavailable => Error.UnavailableError("Test"),
            ErrorType.Unexpected => Error.UnexpectedError("Test"),
            ErrorType.TooManyRequests => Error.TooManyRequestsError("Test"),
            ErrorType.CircuitBreakerOpen => Error.CircuitBreakerOpenError("Test"),
            _ => Error.UnexpectedError("Unknown")
        };
    }

    #endregion
}
