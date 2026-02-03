#if NET48
namespace Voyager.Common.Proxy.Diagnostics.TraceContext
{
    using System;
    using System.Collections.Generic;
    using Voyager.Common.Proxy.Diagnostics;

    /// <summary>
    /// Extension methods for OWIN integration with TraceContext.
    /// </summary>
    public static class OwinTraceContextExtensions
    {
        /// <summary>
        /// Creates a proxy request context factory that includes trace context.
        /// </summary>
        /// <param name="traceContextAccessor">The trace context accessor.</param>
        /// <returns>A factory that creates request contexts from OWIN environment.</returns>
        public static Func<IDictionary<string, object>, IProxyRequestContext> CreateRequestContextFactory(
            ITraceContextAccessor traceContextAccessor)
        {
            return env => new TraceContextProxyRequestContext(traceContextAccessor);
        }

        /// <summary>
        /// Creates a proxy request context factory that wraps an existing factory with trace context.
        /// </summary>
        /// <param name="innerFactory">The inner factory to wrap.</param>
        /// <param name="traceContextAccessor">The trace context accessor.</param>
        /// <returns>A factory that creates request contexts with trace context from OWIN environment.</returns>
        public static Func<IDictionary<string, object>, IProxyRequestContext> CreateRequestContextFactory(
            Func<IDictionary<string, object>, IProxyRequestContext> innerFactory,
            ITraceContextAccessor traceContextAccessor)
        {
            return env =>
            {
                var inner = innerFactory(env);
                return new TraceContextProxyRequestContext(inner, traceContextAccessor);
            };
        }
    }
}
#endif
