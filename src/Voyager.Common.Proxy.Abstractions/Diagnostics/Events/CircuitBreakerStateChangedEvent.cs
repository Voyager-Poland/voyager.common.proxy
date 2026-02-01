#if NET48 || NETSTANDARD2_0
using System;
#endif

namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Event emitted when circuit breaker state changes.
    /// </summary>
    public sealed class CircuitBreakerStateChangedEvent
    {
        /// <summary>
        /// Gets or sets the service name (interface name).
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the previous circuit breaker state.
        /// Values: "Closed", "Open", "HalfOpen".
        /// </summary>
        public string OldState { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the new circuit breaker state.
        /// Values: "Closed", "Open", "HalfOpen".
        /// </summary>
        public string NewState { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current failure count.
        /// </summary>
        public int FailureCount { get; set; }

        /// <summary>
        /// Gets or sets the last error type that caused the state change.
        /// </summary>
        public string? LastErrorType { get; set; }

        /// <summary>
        /// Gets or sets the last error message that caused the state change.
        /// </summary>
        public string? LastErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the event occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        // User context (last user who caused the state change)

        /// <summary>
        /// Gets or sets the current user's login.
        /// </summary>
        public string? UserLogin { get; set; }

        /// <summary>
        /// Gets or sets the organizational unit identifier.
        /// </summary>
        public string? UnitId { get; set; }

        /// <summary>
        /// Gets or sets the organizational unit type.
        /// </summary>
        public string? UnitType { get; set; }
    }
}
