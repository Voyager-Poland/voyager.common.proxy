#if NET48
using System.Collections.Generic;
#endif

namespace Voyager.Common.Proxy.Client.Diagnostics
{
    using Voyager.Common.Proxy.Diagnostics;

    /// <summary>
    /// Default request context that returns null for all properties.
    /// Used when no custom request context is registered.
    /// </summary>
    public sealed class NullProxyRequestContext : IProxyRequestContext
    {
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static NullProxyRequestContext Instance { get; } = new NullProxyRequestContext();

        private NullProxyRequestContext()
        {
        }

        /// <inheritdoc />
        public string? UserLogin => null;

        /// <inheritdoc />
        public string? UnitId => null;

        /// <inheritdoc />
        public string? UnitType => null;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string>? CustomProperties => null;
    }
}
