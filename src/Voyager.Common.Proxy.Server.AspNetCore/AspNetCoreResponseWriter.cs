namespace Voyager.Common.Proxy.Server.AspNetCore;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Adapts ASP.NET Core HttpResponse to IResponseWriter.
/// </summary>
internal sealed class AspNetCoreResponseWriter : IResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpResponse _response;

    public AspNetCoreResponseWriter(HttpResponse response)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
    }

    public async Task WriteJsonAsync<T>(T value, int statusCode)
    {
        _response.StatusCode = statusCode;
        _response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(_response.Body, value, JsonOptions);
    }

    public async Task WriteErrorAsync(string errorType, string message)
    {
        var statusCode = MapErrorTypeToStatusCode(errorType);
        _response.StatusCode = statusCode;
        _response.ContentType = "application/json";

        var error = new { error = message };
        await JsonSerializer.SerializeAsync(_response.Body, error, JsonOptions);
    }

    public Task WriteNoContentAsync()
    {
        _response.StatusCode = StatusCodes.Status204NoContent;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps ErrorType to HTTP status code according to ADR-007.
    /// </summary>
    /// <remarks>
    /// Classification:
    /// - Transient (retryable by client): 408, 429, 503, 504
    /// - Infrastructure (circuit breaker counts): 500, 503, 504, 408
    /// - Business (no retry, CB ignores): 400, 401, 403, 404, 409
    /// </remarks>
    private static int MapErrorTypeToStatusCode(string errorType)
    {
        return errorType switch
        {
            // Business errors - no retry, circuit breaker ignores
            "Validation" => StatusCodes.Status400BadRequest,
            "Business" => StatusCodes.Status400BadRequest,
            "NotFound" => StatusCodes.Status404NotFound,
            "Unauthorized" => StatusCodes.Status401Unauthorized,
            "Permission" => StatusCodes.Status403Forbidden,
            "Forbidden" => StatusCodes.Status403Forbidden,
            "Conflict" => StatusCodes.Status409Conflict,
            "Cancelled" => 499, // Client Closed Request (nginx convention)

            // Transient errors - client may retry, circuit breaker counts
            "Timeout" => StatusCodes.Status504GatewayTimeout,
            "Unavailable" => StatusCodes.Status503ServiceUnavailable,
            "CircuitBreakerOpen" => StatusCodes.Status503ServiceUnavailable,
            "TooManyRequests" => StatusCodes.Status429TooManyRequests,
            "ServiceUnavailable" => StatusCodes.Status503ServiceUnavailable,

            // Infrastructure errors - no retry, but circuit breaker counts
            "Database" => StatusCodes.Status500InternalServerError,
            "Unexpected" => StatusCodes.Status500InternalServerError,
            "Internal" => StatusCodes.Status500InternalServerError,
            "NotImplemented" => StatusCodes.Status501NotImplemented,

            _ => StatusCodes.Status500InternalServerError
        };
    }
}
