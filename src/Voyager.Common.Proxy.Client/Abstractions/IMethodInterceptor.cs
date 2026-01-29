namespace Voyager.Common.Proxy.Client.Abstractions
{
    using System.Reflection;
    using System.Threading.Tasks;

    /// <summary>
    /// Intercepts method calls on a proxy and returns the result.
    /// </summary>
    /// <remarks>
    /// This interface follows the Single Responsibility Principle (SRP) -
    /// it is only responsible for intercepting method calls, not for creating proxies.
    /// </remarks>
    public interface IMethodInterceptor
    {
        /// <summary>
        /// Intercepts a method call and returns the result.
        /// </summary>
        /// <param name="method">The method being called.</param>
        /// <param name="arguments">The arguments passed to the method.</param>
        /// <returns>
        /// A task that represents the asynchronous operation.
        /// The task result contains the return value of the intercepted method.
        /// </returns>
        Task<object?> InterceptAsync(MethodInfo method, object?[] arguments);
    }
}
