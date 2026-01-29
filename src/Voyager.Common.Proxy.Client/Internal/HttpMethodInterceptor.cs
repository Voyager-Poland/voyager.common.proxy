namespace Voyager.Common.Proxy.Client.Internal
{
    using System;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Voyager.Common.Proxy.Client.Abstractions;
    using Voyager.Common.Results;

    using ProxyHttpMethod = Voyager.Common.Proxy.Abstractions.HttpMethod;

    /// <summary>
    /// Intercepts method calls and translates them to HTTP requests.
    /// </summary>
    /// <remarks>
    /// This class follows the Single Responsibility Principle (SRP) -
    /// it is only responsible for HTTP communication, not for proxy creation.
    /// </remarks>
    internal sealed class HttpMethodInterceptor : IMethodInterceptor
    {
        private readonly HttpClient _httpClient;
        private readonly string _servicePrefix;
        private readonly JsonSerializerOptions _jsonOptions;

        public HttpMethodInterceptor(HttpClient httpClient, ServiceProxyOptions options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _servicePrefix = RouteBuilder.GetServicePrefix(typeof(HttpMethodInterceptor).DeclaringType ?? typeof(object));
            _jsonOptions = options.JsonSerializerOptions ?? ServiceProxyOptions.DefaultJsonSerializerOptions;
        }

        public HttpMethodInterceptor(HttpClient httpClient, ServiceProxyOptions options, Type serviceType)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (serviceType is null)
            {
                throw new ArgumentNullException(nameof(serviceType));
            }

            _servicePrefix = RouteBuilder.GetServicePrefix(serviceType);
            _jsonOptions = options.JsonSerializerOptions ?? ServiceProxyOptions.DefaultJsonSerializerOptions;
        }

        /// <inheritdoc/>
        public async Task<object?> InterceptAsync(MethodInfo method, object?[] arguments)
        {
            if (method is null)
            {
                throw new ArgumentNullException(nameof(method));
            }

            arguments ??= Array.Empty<object>();

            // Validate return type
            var returnType = method.ReturnType;

            if (!typeof(Task).IsAssignableFrom(returnType))
            {
                throw new NotSupportedException(
                    $"Method {method.Name} must return Task<Result<T>> or Task<Result>. " +
                    "Synchronous methods are not supported.");
            }

            // Get the inner type (Result<T> or Result)
            var resultType = returnType.IsGenericType
                ? returnType.GetGenericArguments()[0]
                : typeof(Result);

            if (!IsResultType(resultType))
            {
                throw new NotSupportedException(
                    $"Method {method.Name} must return Task<Result<T>> or Task<Result>. " +
                    $"Found: {returnType.Name}");
            }

            return await ExecuteHttpRequestAsync(method, arguments, resultType).ConfigureAwait(false);
        }

        private async Task<object> ExecuteHttpRequestAsync(MethodInfo method, object?[] args, Type resultType)
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

        private static System.Net.Http.HttpMethod ToHttpMethod(ProxyHttpMethod method)
        {
            return method switch
            {
                ProxyHttpMethod.Get => System.Net.Http.HttpMethod.Get,
                ProxyHttpMethod.Post => System.Net.Http.HttpMethod.Post,
                ProxyHttpMethod.Put => System.Net.Http.HttpMethod.Put,
                ProxyHttpMethod.Delete => System.Net.Http.HttpMethod.Delete,
                ProxyHttpMethod.Patch => new System.Net.Http.HttpMethod("PATCH"),
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
