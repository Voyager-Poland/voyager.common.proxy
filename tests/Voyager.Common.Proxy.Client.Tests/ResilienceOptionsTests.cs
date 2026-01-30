namespace Voyager.Common.Proxy.Client.Tests;

using FluentAssertions;
using Xunit;

public class ResilienceOptionsTests
{
    #region Default Values

    [Fact]
    public void RetryOptions_DefaultValues_AreCorrect()
    {
        var options = new RetryOptions();

        options.MaxAttempts.Should().Be(3);
        options.BaseDelayMs.Should().Be(1000);
        options.Enabled.Should().BeFalse();
    }

    [Fact]
    public void CircuitBreakerOptions_DefaultValues_AreCorrect()
    {
        var options = new CircuitBreakerOptions();

        options.FailureThreshold.Should().Be(5);
        options.OpenTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.HalfOpenSuccessThreshold.Should().Be(3);
        options.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ResilienceOptions_ContainsRetryAndCircuitBreaker()
    {
        var options = new ResilienceOptions();

        options.Retry.Should().NotBeNull();
        options.CircuitBreaker.Should().NotBeNull();
    }

    #endregion

    #region ServiceProxyOptions Integration

    [Fact]
    public void ServiceProxyOptions_HasResilienceOptions()
    {
        var options = new ServiceProxyOptions();

        options.Resilience.Should().NotBeNull();
        options.Resilience.Retry.Should().NotBeNull();
        options.Resilience.CircuitBreaker.Should().NotBeNull();
    }

    [Fact]
    public void ServiceProxyOptions_ResilienceCanBeConfigured()
    {
        var options = new ServiceProxyOptions();

        options.Resilience.Retry.Enabled = true;
        options.Resilience.Retry.MaxAttempts = 5;
        options.Resilience.Retry.BaseDelayMs = 500;

        options.Resilience.CircuitBreaker.Enabled = true;
        options.Resilience.CircuitBreaker.FailureThreshold = 10;
        options.Resilience.CircuitBreaker.OpenTimeout = TimeSpan.FromMinutes(1);

        options.Resilience.Retry.Enabled.Should().BeTrue();
        options.Resilience.Retry.MaxAttempts.Should().Be(5);
        options.Resilience.Retry.BaseDelayMs.Should().Be(500);

        options.Resilience.CircuitBreaker.Enabled.Should().BeTrue();
        options.Resilience.CircuitBreaker.FailureThreshold.Should().Be(10);
        options.Resilience.CircuitBreaker.OpenTimeout.Should().Be(TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Exponential Backoff Calculation

    [Theory]
    [InlineData(1000, 1, 1000)]   // 1000 * 2^0 = 1000
    [InlineData(1000, 2, 2000)]   // 1000 * 2^1 = 2000
    [InlineData(1000, 3, 4000)]   // 1000 * 2^2 = 4000
    [InlineData(1000, 4, 8000)]   // 1000 * 2^3 = 8000
    [InlineData(500, 1, 500)]     // 500 * 2^0 = 500
    [InlineData(500, 2, 1000)]    // 500 * 2^1 = 1000
    [InlineData(500, 3, 2000)]    // 500 * 2^2 = 2000
    public void ExponentialBackoff_CalculatesCorrectDelay(int baseDelayMs, int attempt, int expectedDelay)
    {
        // This tests the formula: baseDelay * 2^(attempt-1)
        int actualDelay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
        actualDelay.Should().Be(expectedDelay);
    }

    #endregion
}
