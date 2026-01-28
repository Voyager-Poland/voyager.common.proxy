namespace Voyager.Common.Proxy.Client
{
    using System;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Voyager.Common.Proxy.Client.Internal;
    using Voyager.Common.Results;

    /// <summary>
    /// Dynamic proxy that translates interface method calls to HTTP requests.
    /// </summary>
    /// <typeparam name="TService">The service interface type.</typeparam>
    public class ServiceProxy<TService> : DispatchProxy
        where TService : class
    {
        private HttpClient _httpClient = null!;
        private ServiceProxyOptions _options = null!;
        private string _servicePrefix = null!;
        private JsonSerializerOptions _jsonOptions = null!;

        /// <summary>
        /// Creates a new instance of the service proxy.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use for requests.</param>
        /// <param name="options">The proxy configuration options.</param>
        /// <returns>A proxy instance that implements <typeparamref name="TService"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="httpClient"/> or <paramref name="options"/> is null.
        /// </exception>
        public static TService Create(HttpClient httpClient, ServiceProxyOptions options)
        {
            if (httpClient is null)
            {
                throw new ArgumentNullException(nameof(httpClient));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // Create the proxy
            object proxy = Create<TService, ServiceProxy<TService>>();
            var serviceProxy = (ServiceProxy<TService>)proxy;

            serviceProxy._httpClient = httpClient;
            serviceProxy._options = options;
            serviceProxy._servicePrefix = RouteBuilder.GetServicePrefix(typeof(TService));
            serviceProxy._jsonOptions = options.JsonSerializerOptions ?? ServiceProxyOptions.DefaultJsonSerializerOptions;

            return (TService)proxy;
        }

        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
            {
                throw new ArgumentNullException(nameof(targetMethod));
            }

            args ??= Array.Empty<object>();

            // Check if method returns Task<Result<T>> or Task<Result>
            var returnType = targetMethod.ReturnType;

            if (!typeof(Task).IsAssignableFrom(returnType))
            {
                throw new NotSupportedException(
                    $"Method {targetMethod.Name} must return Task<Result<T>> or Task<Result>. " +
                    $"Synchronous methods are not supported.");
            }

            // Get the inner type (Result<T> or Result)
            var resultType = returnType.IsGenericType
                ? returnType.GetGenericArguments()[0]
                : typeof(Result);

            if (!IsResultType(resultType))
            {
                throw new NotSupportedException(
                    $"Method {targetMethod.Name} must return Task<Result<T>> or Task<Result>. " +
                    $"Found: {returnType.Name}");
            }

            // Execute async
            return InvokeAsyncCore(targetMethod, args, resultType);
        }

        private async Task<object> InvokeAsyncCore(MethodInfo method, object?[] args, Type resultType)
        {
            try
            {
                // Build request
                var httpMethod = RouteBuilder.GetHttpMethod(method);
                var (path, body) = RouteBuilder.BuildRequest(method, args, _servicePrefix);

                // Find CancellationToken in args
                var cancellationToken = FindCancellationToken(method, args);

                // Create HTTP request
                using var request = new HttpRequestMessage(ToHttpMethod(httpMethod), path);

                if (body != null)
                {
                    var json = JsonSerializer.Serialize(body, _jsonOptions);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                // Send request
                using var response = await _httpClient
                    .SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                // Map response to Result
                return await ResultMapper
                    .MapResponseAsync(response, resultType, _jsonOptions)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                return CreateTimeoutResult(resultType);
            }
            catch (OperationCanceledException)
            {
                return CreateCancelledResult(resultType);
            }
            catch (HttpRequestException ex)
            {
                return CreateConnectionErrorResult(resultType, ex.Message);
            }
            catch (Exception ex)
            {
                return CreateUnexpectedErrorResult(resultType, ex.Message);
            }
        }

        private static CancellationToken FindCancellationToken(MethodInfo method, object?[] args)
        {
            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(CancellationToken) && args[i] is CancellationToken ct)
                {
                    return ct;
                }
            }
            return CancellationToken.None;
        }

        private static System.Net.Http.HttpMethod ToHttpMethod(Abstractions.HttpMethod method)
        {
            return method switch
            {
                Abstractions.HttpMethod.Get => System.Net.Http.HttpMethod.Get,
                Abstractions.HttpMethod.Post => System.Net.Http.HttpMethod.Post,
                Abstractions.HttpMethod.Put => System.Net.Http.HttpMethod.Put,
                Abstractions.HttpMethod.Delete => System.Net.Http.HttpMethod.Delete,
                Abstractions.HttpMethod.Patch => new System.Net.Http.HttpMethod("PATCH"),
                _ => throw new ArgumentOutOfRangeException(nameof(method))
            };
        }

        private static bool IsResultType(Type type)
        {
            if (type == typeof(Result))
            {
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<>))
            {
                return true;
            }

            return false;
        }

        private static object CreateCancelledResult(Type resultType)
        {
            var error = Error.CancelledError("The operation was cancelled.");
            return CreateFailureResult(resultType, error);
        }

        private static object CreateTimeoutResult(Type resultType)
        {
            var error = Error.TimeoutError("The request timed out.");
            return CreateFailureResult(resultType, error);
        }

        private static object CreateConnectionErrorResult(Type resultType, string message)
        {
            var error = Error.UnavailableError($"Connection error: {message}");
            return CreateFailureResult(resultType, error);
        }

        private static object CreateUnexpectedErrorResult(Type resultType, string message)
        {
            var error = Error.UnexpectedError($"Unexpected error: {message}");
            return CreateFailureResult(resultType, error);
        }

        private static object CreateFailureResult(Type resultType, Error error)
        {
            if (resultType == typeof(Result))
            {
                return Result.Failure(error);
            }

            // Result<T>.Failure(error)
            var failureMethod = resultType.GetMethod(
                "Failure",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Error) },
                null);

            if (failureMethod != null)
            {
                return failureMethod.Invoke(null, new object[] { error })!;
            }

            // Try implicit conversion
            var implicitOp = resultType.GetMethod(
                "op_Implicit",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Error) },
                null);

            if (implicitOp != null)
            {
                return implicitOp.Invoke(null, new object[] { error })!;
            }

            throw new InvalidOperationException($"Cannot create failure result for type {resultType.Name}");
        }
    }
}
