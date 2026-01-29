namespace Voyager.Common.Proxy.Server.Owin;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Voyager.Common.Proxy.Server.Abstractions;

/// <summary>
/// Adapts the OWIN environment dictionary to IResponseWriter.
/// </summary>
/// <remarks>
/// Uses raw OWIN environment keys for maximum compatibility:
/// - owin.ResponseStatusCode: HTTP status code
/// - owin.ResponseHeaders: Response headers dictionary
/// - owin.ResponseBody: Response body stream
/// </remarks>
internal sealed class OwinResponseWriter : IResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDictionary<string, object> _environment;

    /// <summary>
    /// Creates a new OWIN response writer.
    /// </summary>
    /// <param name="environment">The OWIN environment dictionary.</param>
    public OwinResponseWriter(IDictionary<string, object> environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <inheritdoc />
    public async Task WriteJsonAsync<T>(T value, int statusCode)
    {
        SetStatusCode(statusCode);
        SetContentType("application/json");

        var body = GetResponseBody();
        await JsonSerializer.SerializeAsync(body, value, JsonOptions).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteErrorAsync(string errorType, string message)
    {
        var statusCode = MapErrorTypeToStatusCode(errorType);
        SetStatusCode(statusCode);
        SetContentType("application/json");

        var body = GetResponseBody();
        var error = new { error = message };
        await JsonSerializer.SerializeAsync(body, error, JsonOptions).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task WriteNoContentAsync()
    {
        SetStatusCode(204);
        return Task.CompletedTask;
    }

    private void SetStatusCode(int statusCode)
    {
        _environment["owin.ResponseStatusCode"] = statusCode;
    }

    private void SetContentType(string contentType)
    {
        if (!_environment.TryGetValue("owin.ResponseHeaders", out var headersObj) ||
            headersObj is not IDictionary<string, string[]> headers)
        {
            headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            _environment["owin.ResponseHeaders"] = headers;
        }

        headers["Content-Type"] = new[] { contentType };
    }

    private Stream GetResponseBody()
    {
        if (_environment.TryGetValue("owin.ResponseBody", out var bodyObj) && bodyObj is Stream body)
        {
            return body;
        }

        throw new InvalidOperationException("OWIN response body stream not found in environment.");
    }

    private static int MapErrorTypeToStatusCode(string errorType)
    {
        return errorType switch
        {
            "Validation" => 400,
            "NotFound" => 404,
            "Unauthorized" => 401,
            "Forbidden" => 403,
            "Conflict" => 409,
            "TooManyRequests" => 429,
            "Internal" => 500,
            "NotImplemented" => 501,
            "ServiceUnavailable" => 503,
            _ => 500
        };
    }
}
