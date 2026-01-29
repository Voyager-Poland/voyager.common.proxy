namespace Voyager.Common.Proxy.Server.Abstractions;

using System.Threading.Tasks;

/// <summary>
/// Abstracts the HTTP response writer across different platforms (ASP.NET Core, OWIN).
/// </summary>
public interface IResponseWriter
{
    /// <summary>
    /// Writes a JSON response with the specified status code.
    /// </summary>
    /// <typeparam name="T">The type of value to serialize.</typeparam>
    /// <param name="value">The value to serialize as JSON.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    Task WriteJsonAsync<T>(T value, int statusCode);

    /// <summary>
    /// Writes an error response based on the error type.
    /// </summary>
    /// <param name="errorType">The type of error.</param>
    /// <param name="message">The error message.</param>
    Task WriteErrorAsync(string errorType, string message);

    /// <summary>
    /// Writes a 204 No Content response.
    /// </summary>
    Task WriteNoContentAsync();
}
