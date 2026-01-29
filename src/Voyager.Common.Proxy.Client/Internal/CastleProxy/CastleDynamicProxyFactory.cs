#if NET48
namespace Voyager.Common.Proxy.Client.Internal.CastleProxy
{
    using Castle.DynamicProxy;
    using Voyager.Common.Proxy.Client.Abstractions;

    /// <summary>
    /// Factory that creates proxies using Castle.DynamicProxy.
    /// </summary>
    /// <remarks>
    /// This implementation is only available on .NET Framework 4.8.
    /// It follows the Liskov Substitution Principle (LSP) -
    /// it can be used interchangeably with DispatchProxyFactory (.NET 6.0+).
    /// </remarks>
    internal sealed class CastleDynamicProxyFactory : IProxyFactory
    {
        private static readonly ProxyGenerator ProxyGenerator = new ProxyGenerator();

        /// <inheritdoc/>
        public TService CreateProxy<TService>(IMethodInterceptor interceptor)
            where TService : class
        {
            var adapter = new CastleInterceptorAdapter(interceptor);
            return ProxyGenerator.CreateInterfaceProxyWithoutTarget<TService>(adapter);
        }
    }
}
#endif
