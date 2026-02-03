namespace Voyager.Common.Proxy.Server.Core;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Diagnostics;
using Voyager.Common.Proxy.Server.Abstractions;
using Voyager.Common.Proxy.Server.Core.Diagnostics;

/// <summary>
/// Dispatches HTTP requests to service methods and writes responses.
/// </summary>
public class RequestDispatcher
{
    private readonly ParameterBinder _parameterBinder;

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestDispatcher"/> class.
    /// </summary>
    public RequestDispatcher()
    {
        _parameterBinder = new ParameterBinder();
    }

    /// <summary>
    /// Dispatches a request to the service method and writes the response.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <param name="responseWriter">The response writer.</param>
    /// <param name="endpoint">The endpoint descriptor.</param>
    /// <param name="serviceInstance">The service instance to invoke.</param>
    public Task DispatchAsync(
        IRequestContext context,
        IResponseWriter responseWriter,
        EndpointDescriptor endpoint,
        object serviceInstance)
    {
        return DispatchAsync(context, responseWriter, endpoint, serviceInstance, null, null);
    }

    /// <summary>
    /// Dispatches a request to the service method and writes the response with diagnostics.
    /// </summary>
    /// <param name="context">The request context.</param>
    /// <param name="responseWriter">The response writer.</param>
    /// <param name="endpoint">The endpoint descriptor.</param>
    /// <param name="serviceInstance">The service instance to invoke.</param>
    /// <param name="diagnosticsHandlers">Optional diagnostics handlers.</param>
    /// <param name="requestContext">Optional request context for user information.</param>
    public async Task DispatchAsync(
        IRequestContext context,
        IResponseWriter responseWriter,
        EndpointDescriptor endpoint,
        object serviceInstance,
        IEnumerable<IProxyDiagnostics>? diagnosticsHandlers,
        IProxyRequestContext? requestContext)
    {
        var emitter = new ServerDiagnosticsEmitter(diagnosticsHandlers, requestContext);
        var userContext = emitter.CaptureUserContext();
        var traceContext = ServerDiagnosticsEmitter.GetTraceContext();
        var stopwatch = Stopwatch.StartNew();
        var serviceName = endpoint.ServiceType.Name;
        var methodName = endpoint.Method.Name;
        var httpMethod = endpoint.HttpMethod;
        var url = endpoint.RouteTemplate;

        // Emit RequestStarting
        emitter.EmitRequestStarting(new RequestStartingEvent
        {
            ServiceName = serviceName,
            MethodName = methodName,
            HttpMethod = httpMethod,
            Url = url,
            TraceId = traceContext.TraceId,
            SpanId = traceContext.SpanId,
            ParentSpanId = traceContext.ParentSpanId,
            UserLogin = userContext.UserLogin,
            UnitId = userContext.UnitId,
            UnitType = userContext.UnitType,
            CustomProperties = userContext.CustomProperties
        });

        try
        {
            // Bind parameters
            var parameters = await _parameterBinder.BindParametersAsync(context, endpoint);

            // Validate request parameters if method/interface has [ValidateRequest]
            if (RequestValidator.ShouldValidate(endpoint))
            {
                RequestValidator.ValidateParameters(parameters);
            }

            // Invoke method
            var result = endpoint.Method.Invoke(serviceInstance, parameters);

            // Await if it's a Task
            if (result is Task task)
            {
                await task;

                // Get the result value if it's Task<T>
                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    var resultProperty = taskType.GetProperty("Result");
                    result = resultProperty?.GetValue(task);
                }
                else
                {
                    result = null;
                }
            }

            // Write response based on Result and emit completed event
            stopwatch.Stop();
            var (statusCode, isSuccess, errorType, errorMessage) = await WriteResultResponseAsync(responseWriter, result, endpoint);

            emitter.EmitRequestCompleted(new RequestCompletedEvent
            {
                ServiceName = serviceName,
                MethodName = methodName,
                HttpMethod = httpMethod,
                Url = url,
                StatusCode = statusCode,
                Duration = stopwatch.Elapsed,
                IsSuccess = isSuccess,
                TraceId = traceContext.TraceId,
                SpanId = traceContext.SpanId,
                ParentSpanId = traceContext.ParentSpanId,
                ErrorType = errorType,
                ErrorMessage = errorMessage,
                UserLogin = userContext.UserLogin,
                UnitId = userContext.UnitId,
                UnitType = userContext.UnitType,
                CustomProperties = userContext.CustomProperties
            });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            stopwatch.Stop();
            await WriteExceptionResponseAsync(responseWriter, ex.InnerException);

            emitter.EmitRequestFailed(new RequestFailedEvent
            {
                ServiceName = serviceName,
                MethodName = methodName,
                HttpMethod = httpMethod,
                Url = url,
                Duration = stopwatch.Elapsed,
                ExceptionType = ex.InnerException.GetType().FullName ?? ex.InnerException.GetType().Name,
                ExceptionMessage = ex.InnerException.Message,
                TraceId = traceContext.TraceId,
                SpanId = traceContext.SpanId,
                ParentSpanId = traceContext.ParentSpanId,
                UserLogin = userContext.UserLogin,
                UnitId = userContext.UnitId,
                UnitType = userContext.UnitType,
                CustomProperties = userContext.CustomProperties
            });
        }
        catch (ArgumentException ex)
        {
            stopwatch.Stop();
            await responseWriter.WriteErrorAsync("Validation", ex.Message);

            emitter.EmitRequestCompleted(new RequestCompletedEvent
            {
                ServiceName = serviceName,
                MethodName = methodName,
                HttpMethod = httpMethod,
                Url = url,
                StatusCode = 400, // Validation errors return 400
                Duration = stopwatch.Elapsed,
                IsSuccess = false,
                TraceId = traceContext.TraceId,
                SpanId = traceContext.SpanId,
                ParentSpanId = traceContext.ParentSpanId,
                ErrorType = "Validation",
                ErrorMessage = ex.Message,
                UserLogin = userContext.UserLogin,
                UnitId = userContext.UnitId,
                UnitType = userContext.UnitType,
                CustomProperties = userContext.CustomProperties
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await WriteExceptionResponseAsync(responseWriter, ex);

            emitter.EmitRequestFailed(new RequestFailedEvent
            {
                ServiceName = serviceName,
                MethodName = methodName,
                HttpMethod = httpMethod,
                Url = url,
                Duration = stopwatch.Elapsed,
                ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
                ExceptionMessage = ex.Message,
                TraceId = traceContext.TraceId,
                SpanId = traceContext.SpanId,
                ParentSpanId = traceContext.ParentSpanId,
                UserLogin = userContext.UserLogin,
                UnitId = userContext.UnitId,
                UnitType = userContext.UnitType,
                CustomProperties = userContext.CustomProperties
            });
        }
    }

    private static async Task<(int StatusCode, bool IsSuccess, string? ErrorType, string? ErrorMessage)> WriteResultResponseAsync(
        IResponseWriter responseWriter,
        object? result,
        EndpointDescriptor endpoint)
    {
        if (result == null)
        {
            await responseWriter.WriteNoContentAsync();
            return (204, true, null, null);
        }

        // Use reflection to access Result properties
        var resultType = result.GetType();

        // Get IsSuccess property
        var isSuccessProperty = resultType.GetProperty("IsSuccess");
        var isSuccess = (bool)(isSuccessProperty?.GetValue(result) ?? false);

        if (isSuccess)
        {
            // Success path
            if (endpoint.ResultValueType != null)
            {
                // Result<T> - get Value property
                var valueProperty = resultType.GetProperty("Value");
                var value = valueProperty?.GetValue(result);

                if (value != null)
                {
                    await responseWriter.WriteJsonAsync(value, 200);
                    return (200, true, null, null);
                }
                else
                {
                    await responseWriter.WriteNoContentAsync();
                    return (204, true, null, null);
                }
            }
            else
            {
                // Result (non-generic) - no content
                await responseWriter.WriteNoContentAsync();
                return (204, true, null, null);
            }
        }
        else
        {
            // Error path - get Error property
            var errorProperty = resultType.GetProperty("Error");
            var error = errorProperty?.GetValue(result);

            if (error != null)
            {
                var errorObjType = error.GetType();
                var typeProperty = errorObjType.GetProperty("Type");
                var messageProperty = errorObjType.GetProperty("Message");

                var errorTypeValue = typeProperty?.GetValue(error);
                var errorMessage = messageProperty?.GetValue(error)?.ToString() ?? "An error occurred";

                var errorTypeName = errorTypeValue?.ToString() ?? "Unknown";
                await responseWriter.WriteErrorAsync(errorTypeName, errorMessage);

                // Get HTTP status code from error type (approximate)
                var statusCode = GetStatusCodeForErrorType(errorTypeName);
                return (statusCode, false, errorTypeName, errorMessage);
            }
            else
            {
                await responseWriter.WriteErrorAsync("Unknown", "An unknown error occurred");
                return (500, false, "Unknown", "An unknown error occurred");
            }
        }
    }

    private static int GetStatusCodeForErrorType(string errorType)
    {
        return errorType switch
        {
            "NotFound" => 404,
            "Validation" => 400,
            "Unauthorized" => 401,
            "Forbidden" => 403,
            "Conflict" => 409,
            "TooManyRequests" => 429,
            "Timeout" => 408,
            "Unavailable" => 503,
            "CircuitBreakerOpen" => 503,
            "Internal" => 500,
            _ => 400 // Default to bad request for unknown business errors
        };
    }

    private static async Task WriteExceptionResponseAsync(IResponseWriter responseWriter, Exception exception)
    {
        // Map known exception types to error types
        var errorType = exception switch
        {
            ArgumentException => "Validation",
            UnauthorizedAccessException => "Unauthorized",
            InvalidOperationException => "Conflict",
            NotImplementedException => "NotImplemented",
            _ => "Internal"
        };

        await responseWriter.WriteErrorAsync(errorType, exception.Message);
    }
}
