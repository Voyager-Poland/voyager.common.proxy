namespace Voyager.Common.Proxy.Abstractions
{
    /// <summary>
    /// Specifies that a method should use HTTP GET.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GET is used for retrieving resources. Method parameters are sent as query string parameters
    /// unless they appear in the route template as placeholders.
    /// </para>
    /// <para>
    /// This attribute is optional. Methods starting with Get*, Find*, or List* automatically use GET.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public interface IUserService
    /// {
    ///     // Explicit route template
    ///     [HttpGet("/users/{id}")]
    ///     Task&lt;Result&lt;User&gt;&gt; GetUserAsync(int id);
    ///
    ///     // Query string parameters
    ///     [HttpGet("/users")]
    ///     Task&lt;Result&lt;List&lt;User&gt;&gt;&gt; SearchUsersAsync(string? name, int? limit);
    ///     // Results in: GET /users?name=John&amp;limit=10
    ///
    ///     // Convention-based (no attribute needed)
    ///     Task&lt;Result&lt;User&gt;&gt; GetUserByEmailAsync(string email);
    ///     // Results in: GET /user-service/get-user-by-email?email=...
    /// }
    /// </code>
    /// </example>
    public sealed class HttpGetAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpGetAttribute"/> class.
        /// </summary>
        /// <param name="template">
        /// The route template. If null, the route is derived from the method name.
        /// </param>
        public HttpGetAttribute(string? template = null)
            : base(HttpMethod.Get, template)
        {
        }
    }
}
