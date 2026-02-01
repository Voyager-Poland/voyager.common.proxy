#if NET48 || NETSTANDARD2_0
using System;
using System.Collections.Generic;
#endif

namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Event emitted when a request completes (success or business error).
    /// </summary>
    public sealed class RequestCompletedEvent
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
        /// Gets or sets the HTTP method (GET, POST, etc.).
        /// </summary>
        public string HttpMethod { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the request URL (relative path).
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the request duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets whether the Result indicates success.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the correlation ID for distributed tracing.
        /// </summary>
        public Guid CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the error type if the result is a failure.
        /// </summary>
        public string? ErrorType { get; set; }

        /// <summary>
        /// Gets or sets the error message if the result is a failure.
        /// </summary>
        public string? ErrorMessage { get; set; }

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
