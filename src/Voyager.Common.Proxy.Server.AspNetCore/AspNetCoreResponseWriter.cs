namespace Voyager.Common.Proxy.Server.AspNetCore;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Voyager.Common.Proxy.Server.Abstractions;
using Voyager.Common.Results;
using Voyager.Common.Results.Extensions;

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
    /// Maps ErrorType string to HTTP status code using centralized classification.
    /// </summary>
    private static int MapErrorTypeToStatusCode(string errorType)
    {
        // Use centralized mapping from Voyager.Common.Results.Extensions
        if (Enum.TryParse<ErrorType>(errorType, ignoreCase: true, out var type))
        {
            return type.ToHttpStatusCode();
        }

        // Legacy string mappings for backward compatibility
        return errorType switch
        {
            "Forbidden" => StatusCodes.Status403Forbidden,
            "Internal" => StatusCodes.Status500InternalServerError,
            "ServiceUnavailable" => StatusCodes.Status503ServiceUnavailable,
            "NotImplemented" => StatusCodes.Status501NotImplemented,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}
