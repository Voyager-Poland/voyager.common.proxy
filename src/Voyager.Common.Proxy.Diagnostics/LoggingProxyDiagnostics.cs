namespace Voyager.Common.Proxy.Diagnostics
{
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Diagnostics handler that logs all proxy events using <see cref="ILogger"/>.
    /// </summary>
    /// <remarks>
    /// Log levels:
    /// <list type="bullet">
    /// <item>RequestStarting: Debug</item>
    /// <item>RequestCompleted (success): Debug</item>
    /// <item>RequestCompleted (failure): Warning</item>
    /// <item>RequestFailed: Error</item>
    /// <item>RetryAttempt: Warning</item>
    /// <item>CircuitBreakerStateChanged (→Open): Warning</item>
    /// <item>CircuitBreakerStateChanged (→Closed/HalfOpen): Information</item>
    /// </list>
    /// </remarks>
    public sealed class LoggingProxyDiagnostics : IProxyDiagnostics
    {
        private readonly ILogger<LoggingProxyDiagnostics> _logger;

        /// <summary>
        /// Initializes a new instance of <see cref="LoggingProxyDiagnostics"/>.
        /// </summary>
        /// <param name="logger">The logger instance.</param>
        public LoggingProxyDiagnostics(ILogger<LoggingProxyDiagnostics> logger)
        {
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void OnRequestStarting(RequestStartingEvent e)
        {
            _logger.LogDebug(
                "Proxy request starting: {HttpMethod} {Url} [{ServiceName}.{MethodName}] CorrelationId={CorrelationId} User={UserLogin} Unit={UnitId}/{UnitType}",
                e.HttpMethod,
                e.Url,
                e.ServiceName,
                e.MethodName,
                e.CorrelationId,
                e.UserLogin ?? "(anonymous)",
                e.UnitId ?? "(none)",
                e.UnitType ?? "(none)");
        }

        /// <inheritdoc />
        public void OnRequestCompleted(RequestCompletedEvent e)
        {
            if (e.IsSuccess)
            {
                _logger.LogDebug(
                    "Proxy request completed: {HttpMethod} {Url} {StatusCode} in {Duration}ms [{ServiceName}.{MethodName}] CorrelationId={CorrelationId} User={UserLogin} Unit={UnitId}/{UnitType}",
                    e.HttpMethod,
                    e.Url,
                    e.StatusCode,
                    e.Duration.TotalMilliseconds,
                    e.ServiceName,
                    e.MethodName,
                    e.CorrelationId,
                    e.UserLogin ?? "(anonymous)",
                    e.UnitId ?? "(none)",
                    e.UnitType ?? "(none)");
            }
            else
            {
                _logger.LogWarning(
                    "Proxy request failed: {HttpMethod} {Url} {StatusCode} in {Duration}ms [{ServiceName}.{MethodName}] Error={ErrorType}: {ErrorMessage} CorrelationId={CorrelationId} User={UserLogin} Unit={UnitId}/{UnitType}",
                    e.HttpMethod,
                    e.Url,
                    e.StatusCode,
                    e.Duration.TotalMilliseconds,
                    e.ServiceName,
                    e.MethodName,
                    e.ErrorType,
                    e.ErrorMessage,
                    e.CorrelationId,
                    e.UserLogin ?? "(anonymous)",
                    e.UnitId ?? "(none)",
                    e.UnitType ?? "(none)");
            }
        }

        /// <inheritdoc />
        public void OnRequestFailed(RequestFailedEvent e)
        {
            _logger.LogError(
                "Proxy request exception: {HttpMethod} {Url} in {Duration}ms [{ServiceName}.{MethodName}] Exception={ExceptionType}: {ExceptionMessage} CorrelationId={CorrelationId} User={UserLogin} Unit={UnitId}/{UnitType}",
                e.HttpMethod,
                e.Url,
                e.Duration.TotalMilliseconds,
                e.ServiceName,
                e.MethodName,
                e.ExceptionType,
                e.ExceptionMessage,
                e.CorrelationId,
                e.UserLogin ?? "(anonymous)",
                e.UnitId ?? "(none)",
                e.UnitType ?? "(none)");
        }

        /// <inheritdoc />
        public void OnRetryAttempt(RetryAttemptEvent e)
        {
            _logger.LogWarning(
                "Proxy retry attempt {AttemptNumber}/{MaxAttempts} for [{ServiceName}.{MethodName}] after {ErrorType}: {ErrorMessage}. Waiting {Delay}ms. CorrelationId={CorrelationId} User={UserLogin} Unit={UnitId}/{UnitType}",
                e.AttemptNumber,
                e.MaxAttempts,
                e.ServiceName,
                e.MethodName,
                e.ErrorType,
                e.ErrorMessage,
                e.Delay.TotalMilliseconds,
                e.CorrelationId,
                e.UserLogin ?? "(anonymous)",
                e.UnitId ?? "(none)",
                e.UnitType ?? "(none)");
        }

        /// <inheritdoc />
        public void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
        {
            if (e.NewState == "Open")
            {
                _logger.LogWarning(
                    "Circuit breaker OPENED for {ServiceName}: {OldState} -> {NewState}. Failures={FailureCount}. LastError={LastErrorType}: {LastErrorMessage} User={UserLogin} Unit={UnitId}/{UnitType}",
                    e.ServiceName,
                    e.OldState,
                    e.NewState,
                    e.FailureCount,
                    e.LastErrorType,
                    e.LastErrorMessage,
                    e.UserLogin ?? "(anonymous)",
                    e.UnitId ?? "(none)",
                    e.UnitType ?? "(none)");
            }
            else
            {
                _logger.LogInformation(
                    "Circuit breaker state changed for {ServiceName}: {OldState} -> {NewState}. Failures={FailureCount} User={UserLogin} Unit={UnitId}/{UnitType}",
                    e.ServiceName,
                    e.OldState,
                    e.NewState,
                    e.FailureCount,
                    e.UserLogin ?? "(anonymous)",
                    e.UnitId ?? "(none)",
                    e.UnitType ?? "(none)");
            }
        }
    }
}
