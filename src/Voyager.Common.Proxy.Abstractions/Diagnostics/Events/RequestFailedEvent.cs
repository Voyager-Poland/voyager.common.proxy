#if NET48 || NETSTANDARD2_0
using System;
using System.Collections.Generic;
#endif

namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Event emitted when a request fails with an exception.
    /// </summary>
    public sealed class RequestFailedEvent
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
        /// Gets or sets the request duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the exception type full name.
        /// </summary>
        public string ExceptionType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the exception message.
        /// </summary>
        public string ExceptionMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the W3C Trace ID (32 hex characters).
        /// </summary>
        public string TraceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the W3C Span ID (16 hex characters) - unique per operation.
        /// </summary>
        public string SpanId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the W3C Parent Span ID (16 hex characters).
        /// Null for root spans.
        /// </summary>
        public string? ParentSpanId { get; set; }

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
