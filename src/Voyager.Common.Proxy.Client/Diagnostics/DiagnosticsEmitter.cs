#if NET48
using System;
using System.Collections.Generic;
using System.Diagnostics;
#endif

namespace Voyager.Common.Proxy.Client.Diagnostics
{
    using System.Diagnostics;
    using Voyager.Common.Proxy.Diagnostics;

    /// <summary>
    /// Helper class for emitting diagnostic events safely.
    /// Ensures diagnostics never break the main flow.
    /// </summary>
    internal sealed class DiagnosticsEmitter
    {
        private readonly IEnumerable<IProxyDiagnostics> _handlers;
        private readonly IProxyRequestContext _context;

        public DiagnosticsEmitter(
            IEnumerable<IProxyDiagnostics>? handlers,
            IProxyRequestContext? context)
        {
            _handlers = handlers ?? Array.Empty<IProxyDiagnostics>();
            _context = context ?? NullProxyRequestContext.Instance;
        }

        /// <summary>
        /// Gets the W3C Trace Context for the current request.
        /// Uses Activity.Current if available (OpenTelemetry), otherwise generates new IDs.
        /// </summary>
        public static TraceContext GetTraceContext()
        {
            var activity = Activity.Current;
            if (activity != null)
            {
                return new TraceContext
                {
                    TraceId = activity.TraceId.ToString(),
                    SpanId = activity.SpanId.ToString(),
                    ParentSpanId = activity.ParentSpanId == default ? null : activity.ParentSpanId.ToString()
                };
            }

            // Generate new trace context when no activity exists
            return new TraceContext
            {
                TraceId = Guid.NewGuid().ToString("N"),
                SpanId = Guid.NewGuid().ToString("N").Substring(0, 16),
                ParentSpanId = null
            };
        }

        /// <summary>
        /// W3C Trace Context for distributed tracing.
        /// </summary>
        public sealed class TraceContext
        {
            /// <summary>
            /// W3C Trace ID (32 hex characters).
            /// </summary>
            public string TraceId { get; set; } = string.Empty;

            /// <summary>
            /// W3C Span ID (16 hex characters).
            /// </summary>
            public string SpanId { get; set; } = string.Empty;

            /// <summary>
            /// W3C Parent Span ID (16 hex characters). Null for root spans.
            /// </summary>
            public string? ParentSpanId { get; set; }
        }

        /// <summary>
        /// Captures current user context from IProxyRequestContext.
        /// </summary>
        public UserContext CaptureUserContext()
        {
            return new UserContext
            {
                UserLogin = _context.UserLogin,
                UnitId = _context.UnitId,
                UnitType = _context.UnitType,
                CustomProperties = _context.CustomProperties
            };
        }

        public void EmitRequestStarting(RequestStartingEvent e)
        {
            foreach (var handler in _handlers)
            {
                try
                {
                    handler.OnRequestStarting(e);
                }
                catch
                {
                    // Diagnostics should never break the main flow
                }
            }
        }

        public void EmitRequestCompleted(RequestCompletedEvent e)
        {
            foreach (var handler in _handlers)
            {
                try
                {
                    handler.OnRequestCompleted(e);
                }
                catch
                {
                    // Diagnostics should never break the main flow
                }
            }
        }

        public void EmitRequestFailed(RequestFailedEvent e)
        {
            foreach (var handler in _handlers)
            {
                try
                {
                    handler.OnRequestFailed(e);
                }
                catch
                {
                    // Diagnostics should never break the main flow
                }
            }
        }

        public void EmitRetryAttempt(RetryAttemptEvent e)
        {
            foreach (var handler in _handlers)
            {
                try
                {
                    handler.OnRetryAttempt(e);
                }
                catch
                {
                    // Diagnostics should never break the main flow
                }
            }
        }

        public void EmitCircuitBreakerStateChanged(CircuitBreakerStateChangedEvent e)
        {
            foreach (var handler in _handlers)
            {
                try
                {
                    handler.OnCircuitBreakerStateChanged(e);
                }
                catch
                {
                    // Diagnostics should never break the main flow
                }
            }
        }

        /// <summary>
        /// Captured user context for a request.
        /// </summary>
        public sealed class UserContext
        {
            public string? UserLogin { get; set; }
            public string? UnitId { get; set; }
            public string? UnitType { get; set; }
            public IReadOnlyDictionary<string, string>? CustomProperties { get; set; }
        }
    }
}
