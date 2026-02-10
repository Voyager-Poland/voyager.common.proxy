using System;

namespace Voyager.Common.Proxy.Abstractions;

/// <summary>
/// Specifies a custom response content type for a service method.
/// When applied, the success response will be written with the specified content type
/// instead of the default <c>application/json</c>.
/// </summary>
/// <remarks>
/// <para>
/// This attribute only affects the success path on the server side.
/// Error responses are always returned as <c>application/json</c>.
/// </para>
/// <para>
/// When the result value is a <see cref="string"/>, the raw string value is written
/// directly to the response body without JSON serialization (no wrapping quotes).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [ServiceRoute(ServiceRouteAttribute.NoPrefix)]
/// public interface IPaymentCallbackService
/// {
///     [HttpPost("callback")]
///     [ProducesContentType("text/html")]
///     Task&lt;Result&lt;string&gt;&gt; HandleCallbackAsync(CallbackRequest request);
///     // Success response: 200, Content-Type: text/html, Body: OK
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ProducesContentTypeAttribute : Attribute
{
	/// <summary>
	/// Gets the content type for the success response.
	/// </summary>
	public string ContentType { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ProducesContentTypeAttribute"/> class.
	/// </summary>
	/// <param name="contentType">
	/// The MIME content type for the success response (e.g., "text/html", "text/plain").
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="contentType"/> is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="contentType"/> is empty or contains only whitespace.
	/// </exception>
	public ProducesContentTypeAttribute(string contentType)
	{
		if (contentType is null)
			throw new ArgumentNullException(nameof(contentType));

		if (string.IsNullOrWhiteSpace(contentType))
		{
			throw new ArgumentException(
				"Content type cannot be empty or whitespace.",
				nameof(contentType));
		}

		ContentType = contentType;
	}
}
