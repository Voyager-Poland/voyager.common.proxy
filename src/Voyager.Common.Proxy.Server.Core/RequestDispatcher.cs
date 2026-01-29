namespace Voyager.Common.Proxy.Server.Core;

using System;
using System.Reflection;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Server.Abstractions;

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
    public async Task DispatchAsync(
        IRequestContext context,
        IResponseWriter responseWriter,
        EndpointDescriptor endpoint,
        object serviceInstance)
    {
        try
        {
            // Bind parameters
            var parameters = await _parameterBinder.BindParametersAsync(context, endpoint);

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

            // Write response based on Result
            await WriteResultResponseAsync(responseWriter, result, endpoint);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            await WriteExceptionResponseAsync(responseWriter, ex.InnerException);
        }
        catch (ArgumentException ex)
        {
            await responseWriter.WriteErrorAsync("Validation", ex.Message);
        }
        catch (Exception ex)
        {
            await WriteExceptionResponseAsync(responseWriter, ex);
        }
    }

    private static async Task WriteResultResponseAsync(IResponseWriter responseWriter, object? result, EndpointDescriptor endpoint)
    {
        if (result == null)
        {
            await responseWriter.WriteNoContentAsync();
            return;
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
                }
                else
                {
                    await responseWriter.WriteNoContentAsync();
                }
            }
            else
            {
                // Result (non-generic) - no content
                await responseWriter.WriteNoContentAsync();
            }
        }
        else
        {
            // Error path - get Error property
            var errorProperty = resultType.GetProperty("Error");
            var error = errorProperty?.GetValue(result);

            if (error != null)
            {
                var errorType = error.GetType();
                var typeProperty = errorType.GetProperty("Type");
                var messageProperty = errorType.GetProperty("Message");

                var errorTypeValue = typeProperty?.GetValue(error);
                var errorMessage = messageProperty?.GetValue(error)?.ToString() ?? "An error occurred";

                var errorTypeName = errorTypeValue?.ToString() ?? "Unknown";
                await responseWriter.WriteErrorAsync(errorTypeName, errorMessage);
            }
            else
            {
                await responseWriter.WriteErrorAsync("Unknown", "An unknown error occurred");
            }
        }
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
