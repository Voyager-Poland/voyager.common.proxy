#if NET48
namespace Voyager.Common.Proxy.Client.Internal.CastleProxy
{
    using System;
    using System.Reflection;
    using System.Threading.Tasks;
    using Castle.DynamicProxy;
    using Voyager.Common.Proxy.Client.Abstractions;

    /// <summary>
    /// Adapts <see cref="IMethodInterceptor"/> to Castle's <see cref="IInterceptor"/>.
    /// </summary>
    /// <remarks>
    /// This adapter follows the Interface Segregation Principle (ISP) -
    /// it bridges between two focused interfaces.
    /// </remarks>
    internal sealed class CastleInterceptorAdapter : IInterceptor
    {
        private readonly IMethodInterceptor _interceptor;

        public CastleInterceptorAdapter(IMethodInterceptor interceptor)
        {
            _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
        }

        public void Intercept(IInvocation invocation)
        {
            var returnType = invocation.Method.ReturnType;

            if (returnType == typeof(Task))
            {
                invocation.ReturnValue = InterceptVoidAsync(invocation.Method, invocation.Arguments);
            }
            else if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var resultType = returnType.GetGenericArguments()[0];
                invocation.ReturnValue = InterceptWithResultAsync(invocation.Method, invocation.Arguments, resultType);
            }
            else
            {
                throw new NotSupportedException(
                    $"Method {invocation.Method.Name} must return Task or Task<T>. " +
                    "Synchronous methods are not supported.");
            }
        }

        private async Task InterceptVoidAsync(MethodInfo method, object[] args)
        {
            await _interceptor.InterceptAsync(method, args).ConfigureAwait(false);
        }

        private object InterceptWithResultAsync(MethodInfo method, object[] args, Type resultType)
        {
            // We need to return Task<resultType>, not Task<object?>
            var invokeMethod = typeof(CastleInterceptorAdapter)
                .GetMethod(nameof(InterceptWithResultAsyncCore), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

            return invokeMethod.Invoke(this, new object[] { method, args })!;
        }

        private async Task<T> InterceptWithResultAsyncCore<T>(MethodInfo method, object[] args)
        {
            var result = await _interceptor.InterceptAsync(method, args).ConfigureAwait(false);
            return (T)result!;
        }
    }
}
#endif
