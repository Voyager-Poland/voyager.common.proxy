namespace Voyager.Common.Proxy.Diagnostics.TraceContext
{
    /// <summary>
    /// Provides access to W3C Trace Context information.
    /// </summary>
    /// <remarks>
    /// This interface mirrors the ITraceContextAccessor from Voyager.TraceContext library.
    /// If you have Voyager.TraceContext installed, you can adapt its accessor to this interface.
    /// </remarks>
    public interface ITraceContextAccessor
    {
        /// <summary>
        /// Gets the W3C Trace ID (32-character hex string).
        /// </summary>
        string? TraceId { get; }

        /// <summary>
        /// Gets the W3C Span ID (16-character hex string).
        /// </summary>
        string? SpanId { get; }

        /// <summary>
        /// Gets the parent span ID (16-character hex string), or null for root spans.
        /// </summary>
        string? ParentSpanId { get; }
    }
}
