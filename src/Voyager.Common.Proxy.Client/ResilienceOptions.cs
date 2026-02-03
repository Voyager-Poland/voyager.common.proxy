namespace Voyager.Common.Proxy.Client
{
    using System;

    /// <summary>
    /// Configuration options for retry policy.
    /// </summary>
    public sealed class RetryOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of retry attempts.
        /// Default: 3
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the base delay in milliseconds for exponential backoff.
        /// Actual delays: BaseDelayMs * 2^(attempt-1)
        /// Default: 1000ms (1s, 2s, 4s for 3 attempts)
        /// </summary>
        public int BaseDelayMs { get; set; } = 1000;

        /// <summary>
        /// Gets or sets whether retry is enabled.
        /// Default: false (must be explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Configuration options for circuit breaker policy.
    /// </summary>
    public sealed class CircuitBreakerOptions
    {
        /// <summary>
        /// Gets or sets the number of consecutive infrastructure failures before opening the circuit.
        /// Default: 5
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Gets or sets how long the circuit stays open before allowing test requests.
        /// Default: 30 seconds
        /// </summary>
        public TimeSpan OpenTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the number of successful requests required in half-open state to close the circuit.
        /// Default: 3
        /// </summary>
        public int HalfOpenSuccessThreshold { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether circuit breaker is enabled.
        /// Default: false (must be explicitly enabled)
        /// </summary>
        public bool Enabled { get; set; }
    }

    /// <summary>
    /// Configuration options for resilience policies (retry and circuit breaker).
    /// </summary>
    /// <remarks>
    /// Resilience is applied at the Result level, not HTTP level.
    /// This means it understands error semantics:
    /// - Retry: Only for transient errors (Unavailable, Timeout)
    /// - Circuit Breaker: Counts infrastructure errors (Unavailable, Timeout, Database, Unexpected)
    /// - Business errors (Validation, NotFound, Permission, etc.) are passed through unchanged
    /// </remarks>
    public sealed class ResilienceOptions
    {
        /// <summary>
        /// Gets the retry policy options.
        /// </summary>
        public RetryOptions Retry { get; } = new RetryOptions();

        /// <summary>
        /// Gets the circuit breaker policy options.
        /// </summary>
        public CircuitBreakerOptions CircuitBreaker { get; } = new CircuitBreakerOptions();
    }
}
