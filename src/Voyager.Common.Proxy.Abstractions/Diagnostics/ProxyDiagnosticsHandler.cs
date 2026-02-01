namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Base class for proxy diagnostics handlers.
    /// Override only the events you're interested in - all methods have default empty implementations.
    /// </summary>
    /// <example>
    /// <code>
    /// // Only handle circuit breaker events
    /// public class SlackAlertHandler : ProxyDiagnosticsHandler
    /// {
    ///     public override void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
    ///     {
    ///         if (e.NewState == CircuitState.Open)
    ///             _slack.SendAlert($"Circuit breaker OPEN: {e.ServiceName}");
    ///     }
    ///     // Other methods use default empty implementation from base class
    /// }
    /// </code>
    /// </example>
    public abstract class ProxyDiagnosticsHandler : IProxyDiagnostics
    {
        /// <inheritdoc />
        public virtual void OnRequestStarting(RequestStartingEvent e)
        {
        }

        /// <inheritdoc />
        public virtual void OnRequestCompleted(RequestCompletedEvent e)
        {
        }

        /// <inheritdoc />
        public virtual void OnRequestFailed(RequestFailedEvent e)
        {
        }

        /// <inheritdoc />
        public virtual void OnRetryAttempt(RetryAttemptEvent e)
        {
        }

        /// <inheritdoc />
        public virtual void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
        {
        }
    }
}
