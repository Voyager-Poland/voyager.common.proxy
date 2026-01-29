namespace Voyager.Common.Proxy.Client.Abstractions
{
    /// <summary>
    /// Factory for creating dynamic proxy instances.
    /// </summary>
    /// <remarks>
    /// This interface follows the Dependency Inversion Principle (DIP) -
    /// high-level modules depend on this abstraction rather than concrete implementations.
    /// Different implementations can use DispatchProxy (.NET Core+) or Castle.DynamicProxy (.NET Framework).
    /// </remarks>
    public interface IProxyFactory
    {
        /// <summary>
        /// Creates a proxy instance that implements the specified interface.
        /// </summary>
        /// <typeparam name="TService">The interface type to implement.</typeparam>
        /// <param name="interceptor">The interceptor that handles method calls.</param>
        /// <returns>A proxy instance that implements <typeparamref name="TService"/>.</returns>
        TService CreateProxy<TService>(IMethodInterceptor interceptor)
            where TService : class;
    }
}
