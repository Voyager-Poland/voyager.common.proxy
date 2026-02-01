namespace Voyager.Common.Proxy.Client.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Voyager.Common.Proxy.Client.Abstractions;
    using Voyager.Common.Proxy.Client.Diagnostics;
    using Voyager.Common.Proxy.Diagnostics;
    using Voyager.Common.Resilience;
    using Voyager.Common.Results;
    using Voyager.Common.Results.Extensions;

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
        private readonly string _serviceName;
        private readonly string _servicePrefix;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ResilienceOptions _resilience;
        private readonly CircuitBreakerPolicy? _circuitBreaker;
        private readonly DiagnosticsEmitter _diagnostics;

        public HttpMethodInterceptor(HttpClient httpClient, ServiceProxyOptions options)
            : this(httpClient, options, typeof(HttpMethodInterceptor).DeclaringType ?? typeof(object), null, null, null)
        {
        }

        public HttpMethodInterceptor(HttpClient httpClient, ServiceProxyOptions options, Type serviceType)
            : this(httpClient, options, serviceType, null, null, null)
        {
        }

        public HttpMethodInterceptor(
            HttpClient httpClient,
            ServiceProxyOptions options,
            Type serviceType,
            CircuitBreakerPolicy? circuitBreaker)
            : this(httpClient, options, serviceType, circuitBreaker, null, null)
        {
        }

        public HttpMethodInterceptor(
            HttpClient httpClient,
            ServiceProxyOptions options,
            Type serviceType,
            CircuitBreakerPolicy? circuitBreaker,
            IEnumerable<IProxyDiagnostics>? diagnosticsHandlers,
            IProxyRequestContext? requestContext)
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

            _serviceName = serviceType.Name;
            _servicePrefix = RouteBuilder.GetServicePrefix(serviceType);
            _jsonOptions = options.JsonSerializerOptions ?? ServiceProxyOptions.DefaultJsonSerializerOptions;
            _resilience = options.Resilience;
            _circuitBreaker = circuitBreaker;
            _diagnostics = new DiagnosticsEmitter(diagnosticsHandlers, requestContext);

            // Setup circuit breaker state change callback
            if (_circuitBreaker != null)
            {
                _circuitBreaker.OnStateChanged = OnCircuitBreakerStateChanged;
            }
        }

        private void OnCircuitBreakerStateChanged(CircuitState oldState, CircuitState newState, int failureCount, Error? lastError)
        {
            var userContext = _diagnostics.CaptureUserContext();
            _diagnostics.EmitCircuitBreakerStateChanged(new CircuitBreakerStateChangedEvent
            {
                ServiceName = _serviceName,
                OldState = oldState.ToString(),
                NewState = newState.ToString(),
                FailureCount = failureCount,
                LastErrorType = lastError?.Type.ToString(),
                LastErrorMessage = lastError?.Message,
                UserLogin = userContext.UserLogin,
                UnitId = userContext.UnitId,
                UnitType = userContext.UnitType
            });
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
            return result.Result;
        }

        private async Task<object> ExecuteWithRetryAsync(MethodInfo method, object?[] args, Type resultType)
        {
            int attempt = 0;
            int maxAttempts = _resilience.Retry.MaxAttempts;
            int baseDelayMs = _resilience.Retry.BaseDelayMs;
            HttpRequestResult lastResult;

            // Capture user context once for entire retry sequence
            var userContext = _diagnostics.CaptureUserContext();
            var correlationId = DiagnosticsEmitter.GetCorrelationId();

            while (true)
            {
                attempt++;
                lastResult = await ExecuteHttpRequestAsync(method, args, resultType, correlationId, userContext).ConfigureAwait(false);

                // Check if success
                if (IsSuccessResult(lastResult.Result, resultType))
                {
                    await RecordResultForCircuitBreakerAsync(lastResult.Result, resultType).ConfigureAwait(false);
                    return lastResult.Result;
                }

                // Get error from result
                var error = GetErrorFromResult(lastResult.Result, resultType);

                // Only retry transient errors
                if (error is null || !error.Type.IsTransient() || attempt >= maxAttempts)
                {
                    await RecordResultForCircuitBreakerAsync(lastResult.Result, resultType).ConfigureAwait(false);
                    return lastResult.Result;
                }

                // Calculate delay with exponential backoff
                int delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1);

                // Emit retry event
                _diagnostics.EmitRetryAttempt(new RetryAttemptEvent
                {
                    ServiceName = _serviceName,
                    MethodName = method.Name,
                    AttemptNumber = attempt,
                    MaxAttempts = maxAttempts,
                    Delay = TimeSpan.FromMilliseconds(delayMs),
                    WillRetry = true,
                    ErrorType = error.Type.ToString(),
                    ErrorMessage = error.Message,
                    CorrelationId = correlationId,
                    UserLogin = userContext.UserLogin,
                    UnitId = userContext.UnitId,
                    UnitType = userContext.UnitType,
                    CustomProperties = userContext.CustomProperties
                });

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

        private Task<HttpRequestResult> ExecuteHttpRequestAsync(MethodInfo method, object?[] args, Type resultType)
        {
            var userContext = _diagnostics.CaptureUserContext();
            var correlationId = DiagnosticsEmitter.GetCorrelationId();
            return ExecuteHttpRequestAsync(method, args, resultType, correlationId, userContext);
        }

        private async Task<HttpRequestResult> ExecuteHttpRequestAsync(
            MethodInfo method,
            object?[] args,
            Type resultType,
            Guid correlationId,
            DiagnosticsEmitter.UserContext userContext)
        {
            var httpMethod = RouteBuilder.GetHttpMethod(method);
            var (path, body) = RouteBuilder.BuildRequest(method, args, _servicePrefix);
            var httpMethodString = ToHttpMethod(httpMethod).Method;
            var startTime = Stopwatch.GetTimestamp();

            // Emit request starting
            _diagnostics.EmitRequestStarting(new RequestStartingEvent
            {
                ServiceName = _serviceName,
                MethodName = method.Name,
                HttpMethod = httpMethodString,
                Url = path,
                CorrelationId = correlationId,
                UserLogin = userContext.UserLogin,
                UnitId = userContext.UnitId,
                UnitType = userContext.UnitType,
                CustomProperties = userContext.CustomProperties
            });

            try
            {
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
                var result = await ResultMapper
                    .MapResponseAsync(response, resultType, _jsonOptions)
                    .ConfigureAwait(false);

                var duration = GetElapsed(startTime);
                var isSuccess = IsSuccessResult(result, resultType);
                var error = isSuccess ? null : GetErrorFromResult(result, resultType);

                // Emit request completed
                _diagnostics.EmitRequestCompleted(new RequestCompletedEvent
                {
                    ServiceName = _serviceName,
                    MethodName = method.Name,
                    HttpMethod = httpMethodString,
                    Url = path,
                    StatusCode = (int)response.StatusCode,
                    Duration = duration,
                    IsSuccess = isSuccess,
                    CorrelationId = correlationId,
                    ErrorType = error?.Type.ToString(),
                    ErrorMessage = error?.Message,
                    UserLogin = userContext.UserLogin,
                    UnitId = userContext.UnitId,
                    UnitType = userContext.UnitType,
                    CustomProperties = userContext.CustomProperties
                });

                return new HttpRequestResult(result, (int)response.StatusCode);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                var duration = GetElapsed(startTime);
                EmitRequestFailed(method.Name, httpMethodString, path, duration, ex, correlationId, userContext);
                return new HttpRequestResult(CreateTimeoutResult(resultType), 0);
            }
            catch (OperationCanceledException ex)
            {
                var duration = GetElapsed(startTime);
                EmitRequestFailed(method.Name, httpMethodString, path, duration, ex, correlationId, userContext);
                return new HttpRequestResult(CreateCancelledResult(resultType), 0);
            }
            catch (HttpRequestException ex)
            {
                var duration = GetElapsed(startTime);
                EmitRequestFailed(method.Name, httpMethodString, path, duration, ex, correlationId, userContext);
                return new HttpRequestResult(CreateConnectionErrorResult(resultType, ex.Message), 0);
            }
            catch (Exception ex)
            {
                var duration = GetElapsed(startTime);
                EmitRequestFailed(method.Name, httpMethodString, path, duration, ex, correlationId, userContext);
                return new HttpRequestResult(CreateUnexpectedErrorResult(resultType, ex.Message), 0);
            }
        }

        private void EmitRequestFailed(
            string methodName,
            string httpMethod,
            string url,
            TimeSpan duration,
            Exception ex,
            Guid correlationId,
            DiagnosticsEmitter.UserContext userContext)
        {
            _diagnostics.EmitRequestFailed(new RequestFailedEvent
            {
                ServiceName = _serviceName,
                MethodName = methodName,
                HttpMethod = httpMethod,
                Url = url,
                Duration = duration,
                ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                ExceptionMessage = ex.Message,
                CorrelationId = correlationId,
                UserLogin = userContext.UserLogin,
                UnitId = userContext.UnitId,
                UnitType = userContext.UnitType,
                CustomProperties = userContext.CustomProperties
            });
        }

        private static TimeSpan GetElapsed(long startTimestamp)
        {
            var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            return TimeSpan.FromTicks(elapsed * TimeSpan.TicksPerSecond / Stopwatch.Frequency);
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

        /// <summary>
        /// Result of an HTTP request execution.
        /// </summary>
        private readonly struct HttpRequestResult
        {
            public HttpRequestResult(object result, int statusCode)
            {
                Result = result;
                StatusCode = statusCode;
            }

            public object Result { get; }
            public int StatusCode { get; }
        }
    }
}
