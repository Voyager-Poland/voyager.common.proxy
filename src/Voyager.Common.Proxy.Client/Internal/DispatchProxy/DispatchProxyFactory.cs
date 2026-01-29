#if NET6_0_OR_GREATER
namespace Voyager.Common.Proxy.Client.Internal.DispatchProxy
{
    using Voyager.Common.Proxy.Client.Abstractions;

    /// <summary>
    /// Factory that creates proxies using <see cref="System.Reflection.DispatchProxy"/>.
    /// </summary>
    /// <remarks>
    /// This implementation is only available on .NET 6.0 and later.
    /// It follows the Open/Closed Principle (OCP) - new proxy implementations
    /// can be added without modifying existing code.
    /// </remarks>
    internal sealed class DispatchProxyFactory : IProxyFactory
    {
        /// <inheritdoc/>
        public TService CreateProxy<TService>(IMethodInterceptor interceptor)
            where TService : class
        {
            var proxy = System.Reflection.DispatchProxy.Create<TService, DispatchProxyWrapper<TService>>();
            var wrapper = (DispatchProxyWrapper<TService>)(object)proxy;
            wrapper.SetInterceptor(interceptor);
            return proxy;
        }
    }
}
#endif
