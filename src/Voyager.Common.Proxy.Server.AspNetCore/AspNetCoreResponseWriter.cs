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

    private static int MapErrorTypeToStatusCode(string errorType)
    {
        return errorType switch
        {
            "Validation" => StatusCodes.Status400BadRequest,
            "NotFound" => StatusCodes.Status404NotFound,
            "Unauthorized" => StatusCodes.Status401Unauthorized,
            "Forbidden" => StatusCodes.Status403Forbidden,
            "Conflict" => StatusCodes.Status409Conflict,
            "TooManyRequests" => StatusCodes.Status429TooManyRequests,
            "Internal" => StatusCodes.Status500InternalServerError,
            "NotImplemented" => StatusCodes.Status501NotImplemented,
            "ServiceUnavailable" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError
        };
    }
}
