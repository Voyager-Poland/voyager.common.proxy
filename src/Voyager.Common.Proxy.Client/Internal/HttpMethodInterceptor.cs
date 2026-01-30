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
    using Voyager.Common.Resilience;
    using Voyager.Common.Results;

    using ProxyHttpMethod = Voyager.Common.Proxy.Abstractions.HttpMethod;

    /// <summary>
    /// Intercepts method calls and translates them to HTTP requests.
    /// </summary>
    /// <remarks>
    /// This class follows the Single Responsibility Principle (SRP) -
    /// it is only responsible for HTTP communication, not for proxy creation.
    /// Resilience (retry, circuit breaker) is applied at the Result level
    /// using Voyager.Common.Resilience.
    /// </remarks>
    internal sealed class HttpMethodInterceptor : IMethodInterceptor
    {
        private readonly HttpClient _httpClient;
        private readonly string _servicePrefix;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ResilienceOptions _resilience;
        private readonly CircuitBreakerPolicy? _circuitBreaker;

        public HttpMethodInterceptor(HttpClient httpClient, ServiceProxyOptions options)
            : this(httpClient, options, typeof(HttpMethodInterceptor).DeclaringType ?? typeof(object), null)
        {
        }

        public HttpMethodInterceptor(HttpClient httpClient, ServiceProxyOptions options, Type serviceType)
            : this(httpClient, options, serviceType, null)
        {
        }

        public HttpMethodInterceptor(
            HttpClient httpClient,
            ServiceProxyOptions options,
            Type serviceType,
            CircuitBreakerPolicy? circuitBreaker)
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
            _resilience = options.Resilience;
            _circuitBreaker = circuitBreaker;
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

            return await ExecuteWithResilienceAsync(method, arguments, resultType).ConfigureAwait(false);
        }

        private async Task<object> ExecuteWithResilienceAsync(MethodInfo method, object?[] args, Type resultType)
        {
            // Check circuit breaker first
            if (_circuitBreaker != null)
            {
                var allowResult = await _circuitBreaker.ShouldAllowRequestAsync().ConfigureAwait(false);
                if (allowResult.IsFailure)
                {
                    return CreateFailureResult(resultType, allowResult.Error!);
                }
            }

            // Execute with retry if enabled
            if (_resilience.Retry.Enabled)
            {
                return await ExecuteWithRetryAsync(method, args, resultType).ConfigureAwait(false);
            }

            // Execute without retry
            var result = await ExecuteHttpRequestAsync(method, args, resultType).ConfigureAwait(false);
            await RecordResultForCircuitBreakerAsync(result, resultType).ConfigureAwait(false);
            return result;
        }

        private async Task<object> ExecuteWithRetryAsync(MethodInfo method, object?[] args, Type resultType)
        {
            int attempt = 0;
            int maxAttempts = _resilience.Retry.MaxAttempts;
            int baseDelayMs = _resilience.Retry.BaseDelayMs;
            object lastResult;

            while (true)
            {
                attempt++;
                lastResult = await ExecuteHttpRequestAsync(method, args, resultType).ConfigureAwait(false);

                // Check if success
                if (IsSuccessResult(lastResult, resultType))
                {
                    await RecordResultForCircuitBreakerAsync(lastResult, resultType).ConfigureAwait(false);
                    return lastResult;
                }

                // Get error from result
                var error = GetErrorFromResult(lastResult, resultType);

                // Only retry transient errors
                if (!IsTransientError(error) || attempt >= maxAttempts)
                {
                    await RecordResultForCircuitBreakerAsync(lastResult, resultType).ConfigureAwait(false);
                    return lastResult;
                }

                // Calculate delay with exponential backoff
                int delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1);
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
        }

        private async Task RecordResultForCircuitBreakerAsync(object result, Type resultType)
        {
            if (_circuitBreaker == null)
            {
                return;
            }

            if (IsSuccessResult(result, resultType))
            {
                await _circuitBreaker.RecordSuccessAsync().ConfigureAwait(false);
            }
            else
            {
                var error = GetErrorFromResult(result, resultType);
                if (error != null)
                {
                    await _circuitBreaker.RecordFailureAsync(error).ConfigureAwait(false);
                }
            }
        }

        private static bool IsSuccessResult(object result, Type resultType)
        {
            var isSuccessProperty = resultType.GetProperty("IsSuccess");
            if (isSuccessProperty != null)
            {
                return (bool)(isSuccessProperty.GetValue(result) ?? false);
            }
            return false;
        }

        private static Error? GetErrorFromResult(object result, Type resultType)
        {
            var errorProperty = resultType.GetProperty("Error");
            if (errorProperty != null)
            {
                return errorProperty.GetValue(result) as Error;
            }
            return null;
        }

        private static bool IsTransientError(Error? error)
        {
            if (error == null)
            {
                return false;
            }

            return error.Type == ErrorType.Unavailable
                || error.Type == ErrorType.Timeout;
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
