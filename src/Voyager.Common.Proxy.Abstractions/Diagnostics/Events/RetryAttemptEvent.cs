#if NET48 || NETSTANDARD2_0
using System;
using System.Collections.Generic;
#endif

namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Event emitted when a retry attempt is about to be made.
    /// </summary>
    public sealed class RetryAttemptEvent
    {
        /// <summary>
        /// Gets or sets the service name (interface name).
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the method name being called.
        /// </summary>
        public string MethodName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current attempt number (1-based).
        /// </summary>
        public int AttemptNumber { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of attempts configured.
        /// </summary>
        public int MaxAttempts { get; set; }

        /// <summary>
        /// Gets or sets the delay before the next attempt.
        /// </summary>
        public TimeSpan Delay { get; set; }

        /// <summary>
        /// Gets or sets whether another retry will be attempted.
        /// </summary>
        public bool WillRetry { get; set; }

        /// <summary>
        /// Gets or sets the error type from the failed attempt.
        /// </summary>
        public string ErrorType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the error message from the failed attempt.
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the correlation ID for distributed tracing.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the event occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        // User context

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

        /// <summary>
        /// Gets or sets additional custom properties.
        /// </summary>
        public IReadOnlyDictionary<string, string>? CustomProperties { get; set; }
    }
}
