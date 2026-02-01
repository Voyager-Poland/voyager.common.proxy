#if NET48 || NETSTANDARD2_0
using System.Collections.Generic;
#endif

namespace Voyager.Common.Proxy.Diagnostics
{
    /// <summary>
    /// Provides user context for diagnostic events.
    /// Implement this interface to add user/tenant information to all proxy events.
    /// </summary>
    /// <remarks>
    /// Context is captured once per request and attached to all related events.
    /// Implementation should be thread-safe and fast (called on every request).
    /// </remarks>
    public interface IProxyRequestContext
    {
        /// <summary>
        /// Gets the current user's login. Should always return a value when user is authenticated.
        /// </summary>
        string? UserLogin { get; }

        /// <summary>
        /// Gets the organizational unit identifier (agent ID, broker ID, etc.).
        /// </summary>
        string? UnitId { get; }

        /// <summary>
        /// Gets the organizational unit type (e.g., "Agent", "Akwizytor", "Broker").
        /// Product-specific string, not an enum.
        /// </summary>
        string? UnitType { get; }

        /// <summary>
        /// Gets additional custom properties to include in diagnostic events.
        /// Return null if no custom properties are needed.
        /// </summary>
        IReadOnlyDictionary<string, string>? CustomProperties { get; }
    }
}
