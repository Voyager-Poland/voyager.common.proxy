namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Interface for receiving diagnostic events from the proxy.
    /// Implement this interface to integrate with logging, APM, or metrics systems.
    /// </summary>
    /// <remarks>
    /// For easier implementation, inherit from <see cref="ProxyDiagnosticsHandler"/>
    /// which provides default empty implementations for all methods.
    /// </remarks>
    public interface IProxyDiagnostics
    {
        /// <summary>
        /// Called when an HTTP request is about to be sent.
        /// </summary>
        /// <param name="e">Event containing request details.</param>
        void OnRequestStarting(RequestStartingEvent e);

        /// <summary>
        /// Called when an HTTP request completes (success or business error).
        /// </summary>
        /// <param name="e">Event containing response details.</param>
        void OnRequestCompleted(RequestCompletedEvent e);

        /// <summary>
        /// Called when an HTTP request fails with an exception.
        /// </summary>
        /// <param name="e">Event containing exception details.</param>
        void OnRequestFailed(RequestFailedEvent e);

        /// <summary>
        /// Called when a retry attempt is about to be made.
        /// </summary>
        /// <param name="e">Event containing retry details.</param>
        void OnRetryAttempt(RetryAttemptEvent e);

        /// <summary>
        /// Called when circuit breaker state changes.
        /// </summary>
        /// <param name="e">Event containing state change details.</param>
        void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e);
    }
}
