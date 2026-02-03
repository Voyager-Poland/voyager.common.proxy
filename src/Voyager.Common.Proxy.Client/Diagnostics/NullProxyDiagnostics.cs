namespace Voyager.Common.Proxy.Client.Diagnostics
{
    using Voyager.Common.Proxy.Diagnostics;

    /// <summary>
    /// Default diagnostics handler that does nothing.
    /// Used when no diagnostics handlers are registered.
    /// </summary>
    public sealed class NullProxyDiagnostics : IProxyDiagnostics
    {
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static NullProxyDiagnostics Instance { get; } = new NullProxyDiagnostics();

        private NullProxyDiagnostics()
        {
        }

        /// <inheritdoc />
        public void OnRequestStarting(RequestStartingEvent e)
        {
        }

        /// <inheritdoc />
        public void OnRequestCompleted(RequestCompletedEvent e)
        {
        }

        /// <inheritdoc />
        public void OnRequestFailed(RequestFailedEvent e)
        {
        }

        /// <inheritdoc />
        public void OnRetryAttempt(RetryAttemptEvent e)
        {
        }

        /// <inheritdoc />
        public void OnCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
        {
        }
    }
}
