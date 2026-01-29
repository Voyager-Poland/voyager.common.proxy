#if NET6_0_OR_GREATER
namespace Voyager.Common.Proxy.Client.Internal.DispatchProxy
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Voyager.Common.Proxy.Client.Abstractions;

    /// <summary>
    /// DispatchProxy wrapper that delegates method calls to an <see cref="IMethodInterceptor"/>.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    /// <remarks>
    /// This class follows the Liskov Substitution Principle (LSP) -
    /// it can be used anywhere a TService is expected.
    /// </remarks>
    internal class DispatchProxyWrapper<TService> : System.Reflection.DispatchProxy
        where TService : class
    {
        private IMethodInterceptor? _interceptor;

        /// <summary>
        /// Sets the interceptor for this proxy instance.
        /// </summary>
        /// <param name="interceptor">The interceptor to use.</param>
        internal void SetInterceptor(IMethodInterceptor interceptor)
        {
            _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
        }

        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new ArgumentNullException(nameof(targetMethod));
            }

            if (_interceptor is null)
            {
                throw new InvalidOperationException("Interceptor has not been set. This is a bug in proxy initialization.");
            }

            args ??= Array.Empty<object>();

            // The interceptor returns Task<object?>, but the method might return Task<T>
            // We need to wrap it properly
            var returnType = targetMethod.ReturnType;

            if (returnType == typeof(Task))
            {
                return InvokeVoidAsync(targetMethod, args);
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                return InvokeWithResultAsync(targetMethod, args, resultType);
            }

            // Synchronous methods are not supported
            throw new NotSupportedException(
                $"Method {targetMethod.Name} must return Task or Task<T>. " +
                "Synchronous methods are not supported.");
        }

        private async Task InvokeVoidAsync(MethodInfo method, object?[] args)
        {
            await _interceptor!.InterceptAsync(method, args).ConfigureAwait(false);
        }

        private object InvokeWithResultAsync(MethodInfo method, object?[] args, Type resultType)
        {
            // We need to return Task<resultType>, not Task<object?>
            // Use reflection to call the generic method
            var invokeMethod = typeof(DispatchProxyWrapper<TService>)
                .GetMethod(nameof(InvokeWithResultAsyncCore), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

            return invokeMethod.Invoke(this, new object[] { method, args })!;
        }

        private async Task<T> InvokeWithResultAsyncCore<T>(MethodInfo method, object?[] args)
        {
            var result = await _interceptor!.InterceptAsync(method, args).ConfigureAwait(false);
            return (T)result!;
        }
    }
}
#endif
