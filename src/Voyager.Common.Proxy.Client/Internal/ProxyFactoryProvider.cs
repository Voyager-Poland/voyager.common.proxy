namespace Voyager.Common.Proxy.Client.Internal
{
    using Voyager.Common.Proxy.Client.Abstractions;

    /// <summary>
    /// Provides the appropriate <see cref="IProxyFactory"/> implementation for the current platform.
    /// </summary>
    /// <remarks>
    /// This class follows the Dependency Inversion Principle (DIP) -
    /// it provides an abstraction that high-level modules can depend on,
    /// while the actual implementation is determined at compile time.
    /// </remarks>
    internal static class ProxyFactoryProvider
    {
        private static readonly IProxyFactory Instance = CreateFactory();

        /// <summary>
        /// Gets the proxy factory for the current platform.
        /// </summary>
        /// <returns>An <see cref="IProxyFactory"/> implementation.</returns>
        public static IProxyFactory GetFactory() => Instance;

        private static IProxyFactory CreateFactory()
        {
#if NET48
            return new CastleProxy.CastleDynamicProxyFactory();
#else
            return new DispatchProxy.DispatchProxyFactory();
#endif
        }
    }
}
