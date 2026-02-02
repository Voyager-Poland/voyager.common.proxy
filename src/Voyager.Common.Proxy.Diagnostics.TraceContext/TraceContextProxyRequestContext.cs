using System.Collections.Generic;

namespace Voyager.Common.Proxy.Diagnostics.TraceContext
{
    using Voyager.Common.Proxy.Diagnostics;

    /// <summary>
    /// A proxy request context that includes trace context from <see cref="ITraceContextAccessor"/>.
    /// Wraps an inner <see cref="IProxyRequestContext"/> and adds trace information.
    /// </summary>
    public class TraceContextProxyRequestContext : IProxyRequestContext
    {
        private readonly IProxyRequestContext? _inner;
        private readonly ITraceContextAccessor _traceContextAccessor;

        /// <summary>
        /// Initializes a new instance of <see cref="TraceContextProxyRequestContext"/>.
        /// </summary>
        /// <param name="traceContextAccessor">The trace context accessor.</param>
        public TraceContextProxyRequestContext(ITraceContextAccessor traceContextAccessor)
            : this(null, traceContextAccessor)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TraceContextProxyRequestContext"/>.
        /// </summary>
        /// <param name="inner">The inner request context to wrap.</param>
        /// <param name="traceContextAccessor">The trace context accessor.</param>
        public TraceContextProxyRequestContext(IProxyRequestContext? inner, ITraceContextAccessor traceContextAccessor)
        {
            _inner = inner;
            _traceContextAccessor = traceContextAccessor ?? throw new System.ArgumentNullException(nameof(traceContextAccessor));
        }

        /// <inheritdoc />
        public string? UserLogin => _inner?.UserLogin;

        /// <inheritdoc />
        public string? UnitId => _inner?.UnitId;

        /// <inheritdoc />
        public string? UnitType => _inner?.UnitType;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string>? CustomProperties
        {
            get
            {
                var innerProps = _inner?.CustomProperties;
                var (traceId, spanId, parentSpanId) = TraceContextHelper.GetTraceContext(_traceContextAccessor);

                // Merge trace context into custom properties
                var result = new Dictionary<string, string>();

                if (innerProps != null)
                {
                    foreach (var kvp in innerProps)
                    {
                        result[kvp.Key] = kvp.Value;
                    }
                }

                result["trace.id"] = traceId;
                result["span.id"] = spanId;
                if (parentSpanId != null)
                {
                    result["parent.span.id"] = parentSpanId;
                }

                return result;
            }
        }

        /// <summary>
        /// Gets the current trace ID from the accessor.
        /// </summary>
        public string TraceId => _traceContextAccessor.TraceId ?? string.Empty;

        /// <summary>
        /// Gets the current span ID from the accessor.
        /// </summary>
        public string SpanId => _traceContextAccessor.SpanId ?? string.Empty;

        /// <summary>
        /// Gets the parent span ID from the accessor.
        /// </summary>
        public string? ParentSpanId => _traceContextAccessor.ParentSpanId;
    }
}
