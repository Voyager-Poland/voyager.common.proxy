using System;

namespace Voyager.Common.Proxy.Diagnostics.TraceContext
{
    /// <summary>
    /// Helper class for extracting W3C Trace Context from <see cref="ITraceContextAccessor"/>.
    /// </summary>
    public static class TraceContextHelper
    {
        /// <summary>
        /// Gets trace context from the accessor.
        /// </summary>
        /// <param name="accessor">The trace context accessor.</param>
        /// <returns>Tuple of (TraceId, SpanId, ParentSpanId).</returns>
        public static (string TraceId, string SpanId, string? ParentSpanId) GetTraceContext(ITraceContextAccessor? accessor)
        {
            if (accessor == null)
            {
                return GenerateFallbackContext();
            }

            var traceId = accessor.TraceId;
            var spanId = accessor.SpanId;
            var parentSpanId = accessor.ParentSpanId;

            // If accessor returns empty values, generate fallback
            if (string.IsNullOrEmpty(traceId) || string.IsNullOrEmpty(spanId))
            {
                return GenerateFallbackContext();
            }

            return (traceId!, spanId!, parentSpanId);
        }

        private static (string TraceId, string SpanId, string? ParentSpanId) GenerateFallbackContext()
        {
            return (
                Guid.NewGuid().ToString("N"),
                Guid.NewGuid().ToString("N").Substring(0, 16),
                null
            );
        }
    }
}
