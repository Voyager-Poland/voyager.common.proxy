namespace Voyager.Common.Proxy.Abstractions
{
    /// <summary>
    /// Specifies that a method should use HTTP PUT.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PUT is used for replacing entire resources.
    /// Complex type parameters are sent as JSON in the request body.
    /// </para>
    /// <para>
    /// This attribute is optional. Methods starting with Update* automatically use PUT.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public interface IUserService
    /// {
    ///     // Explicit route template
    ///     [HttpPut("/users/{id}")]
    ///     Task&lt;Result&lt;User&gt;&gt; UpdateUserAsync(int id, UpdateUserRequest request);
    ///
    ///     // Convention-based (no attribute needed)
    ///     Task&lt;Result&lt;User&gt;&gt; UpdateUserAsync(int id, UpdateUserRequest request);
    ///     // Results in: PUT /user-service/update-user?id=123
    /// }
    /// </code>
    /// </example>
    public sealed class HttpPutAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPutAttribute"/> class.
        /// </summary>
        /// <param name="template">
        /// The route template. If null, the route is derived from the method name.
        /// </param>
        public HttpPutAttribute(string? template = null)
            : base(HttpMethod.Put, template)
        {
        }
    }
}
