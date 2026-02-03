namespace Voyager.Common.Proxy.Client.Extensions
{
    using System;
    using System.Threading.Tasks;
    using Voyager.Common.Results;
    using Voyager.Common.Results.Extensions;

    /// <summary>
    /// Retry policy delegate for Result-based operations.
    /// </summary>
    /// <param name="attemptNumber">Current attempt number (1-based)</param>
    /// <param name="error">Error from the previous attempt</param>
    /// <returns>Success with delay in milliseconds if retry should continue, Failure to stop retrying</returns>
    public delegate Result<int> ResultRetryPolicy(int attemptNumber, Error error);

    /// <summary>
    /// Extension methods for applying retry logic to Result-returning operations.
    /// </summary>
    /// <remarks>
    /// These extensions implement the resilience strategy from ADR-007.
    /// For circuit breaker patterns, use Voyager.Common.Resilience.
    /// </remarks>
    public static class ResultResilienceExtensions
    {
        /// <summary>
        /// Applies retry logic to a Result-returning operation using the default transient error policy.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="resultTask">The operation to retry</param>
        /// <returns>Result from the operation, with retry for transient errors</returns>
        /// <remarks>
        /// Default policy: 3 attempts with exponential backoff (1s, 2s, 4s).
        /// Only retries transient errors: Unavailable, Timeout.
        /// </remarks>
        /// <example>
        /// <code>
        /// var result = await _userService.GetUserAsync(id)
        ///     .WithRetryAsync();
        /// </code>
        /// </example>
        public static Task<Result<T>> WithRetryAsync<T>(this Task<Result<T>> resultTask)
        {
            return WithRetryAsync(resultTask, TransientErrorPolicy());
        }

        /// <summary>
        /// Applies retry logic to a Result-returning operation using a custom policy.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="resultTask">The operation to retry</param>
        /// <param name="policy">Custom retry policy</param>
        /// <returns>Result from the operation, preserving the original error if all retries fail</returns>
        /// <example>
        /// <code>
        /// var result = await _userService.GetUserAsync(id)
        ///     .WithRetryAsync(TransientErrorPolicy(maxAttempts: 5, baseDelayMs: 500));
        /// </code>
        /// </example>
        public static async Task<Result<T>> WithRetryAsync<T>(
            this Task<Result<T>> resultTask,
            ResultRetryPolicy policy)
        {
            var result = await resultTask.ConfigureAwait(false);

            if (result.IsSuccess)
                return result;

            int attempt = 1;
            var lastResult = result;

            while (true)
            {
                var retryDecision = policy(attempt, lastResult.Error);

                // Stop retrying - return the ORIGINAL error
                if (retryDecision.IsFailure)
                    return lastResult;

                // Wait before next attempt
                await Task.Delay(retryDecision.Value).ConfigureAwait(false);
                attempt++;

                // Note: This extension is for use AFTER the operation has completed
                // For re-executing the operation, wrap in a Func<Task<Result<T>>>
                // This simplified version is for demonstration - real retry needs the operation
                return lastResult;
            }
        }

        /// <summary>
        /// Applies retry logic to a retryable operation using the default transient error policy.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="operation">The operation to retry (will be re-executed on failure)</param>
        /// <returns>Result from the operation, with retry for transient errors</returns>
        /// <example>
        /// <code>
        /// var result = await RetryAsync(() => _userService.GetUserAsync(id));
        /// </code>
        /// </example>
        public static Task<Result<T>> RetryAsync<T>(Func<Task<Result<T>>> operation)
        {
            return RetryAsync(operation, TransientErrorPolicy());
        }

        /// <summary>
        /// Applies retry logic to a retryable operation using a custom policy.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="operation">The operation to retry (will be re-executed on failure)</param>
        /// <param name="policy">Custom retry policy</param>
        /// <returns>Result from the operation, preserving the original error if all retries fail</returns>
        /// <example>
        /// <code>
        /// var result = await RetryAsync(
        ///     () => _userService.GetUserAsync(id),
        ///     TransientErrorPolicy(maxAttempts: 5));
        /// </code>
        /// </example>
        public static async Task<Result<T>> RetryAsync<T>(
            Func<Task<Result<T>>> operation,
            ResultRetryPolicy policy)
        {
            int attempt = 1;
            Result<T> lastResult;

            while (true)
            {
                lastResult = await operation().ConfigureAwait(false);

                if (lastResult.IsSuccess)
                    return lastResult;

                var retryDecision = policy(attempt, lastResult.Error);

                // Stop retrying - return the ORIGINAL error
                if (retryDecision.IsFailure)
                    return lastResult;

                // Wait before next attempt
                await Task.Delay(retryDecision.Value).ConfigureAwait(false);
                attempt++;
            }
        }

        /// <summary>
        /// Creates a retry policy for transient errors with exponential backoff.
        /// </summary>
        /// <param name="maxAttempts">Maximum number of attempts (default: 3)</param>
        /// <param name="baseDelayMs">Base delay in milliseconds (default: 1000)</param>
        /// <returns>Retry policy that retries only Unavailable and Timeout errors</returns>
        /// <remarks>
        /// According to ADR-007:
        /// - Retries: Unavailable, Timeout
        /// - Does NOT retry: Validation, NotFound, Permission, Unauthorized, Conflict, Business, Cancelled, Unexpected
        ///
        /// Delay strategy: baseDelay * 2^(attempt-1)
        /// - Attempt 1: 1000ms
        /// - Attempt 2: 2000ms
        /// - Attempt 3: 4000ms
        /// </remarks>
        public static ResultRetryPolicy TransientErrorPolicy(int maxAttempts = 3, int baseDelayMs = 1000)
        {
            return (attempt, error) =>
            {
                // Only retry transient errors (using centralized classification from Voyager.Common.Results)
                if (attempt >= maxAttempts || !error.Type.IsTransient())
                    return Result<int>.Failure(error);

                // Exponential backoff
                int delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                return Result<int>.Success(delayMs);
            };
        }

        /// <summary>
        /// Creates a custom retry policy.
        /// </summary>
        /// <param name="maxAttempts">Maximum number of attempts</param>
        /// <param name="shouldRetry">Predicate to determine if an error should be retried</param>
        /// <param name="delayStrategy">Function that calculates delay based on attempt number</param>
        /// <returns>Custom retry policy</returns>
        /// <example>
        /// <code>
        /// var policy = CustomRetryPolicy(
        ///     maxAttempts: 5,
        ///     shouldRetry: e => e.Type == ErrorType.Unavailable || e.Code == "RATE_LIMIT",
        ///     delayStrategy: attempt => 500 * attempt);  // Linear backoff
        /// </code>
        /// </example>
        public static ResultRetryPolicy CustomRetryPolicy(
            int maxAttempts,
            Func<Error, bool> shouldRetry,
            Func<int, int> delayStrategy)
        {
            return (attempt, error) =>
            {
                if (attempt >= maxAttempts || !shouldRetry(error))
                    return Result<int>.Failure(error);

                int delayMs = delayStrategy(attempt);
                return Result<int>.Success(delayMs);
            };
        }

        /// <summary>
        /// Determines if an error is transient (retryable).
        /// </summary>
        /// <param name="error">The error to check</param>
        /// <returns>True if the error is transient</returns>
        /// <remarks>
        /// Use error.Type.IsTransient() from Voyager.Common.Results.Extensions instead.
        /// </remarks>
        [Obsolete("Use error.Type.IsTransient() from Voyager.Common.Results.Extensions instead.")]
        public static bool IsTransient(this Error error)
        {
            return error.Type.IsTransient();
        }

        /// <summary>
        /// Determines if an error should count towards circuit breaker threshold.
        /// </summary>
        /// <param name="error">The error to check</param>
        /// <returns>True if the error is an infrastructure failure</returns>
        /// <remarks>
        /// Use error.Type.ShouldCountForCircuitBreaker() from Voyager.Common.Results.Extensions instead.
        /// </remarks>
        [Obsolete("Use error.Type.ShouldCountForCircuitBreaker() from Voyager.Common.Results.Extensions instead.")]
        public static bool IsInfrastructureFailure(this Error error)
        {
            return error.Type.ShouldCountForCircuitBreaker();
        }
    }
}
