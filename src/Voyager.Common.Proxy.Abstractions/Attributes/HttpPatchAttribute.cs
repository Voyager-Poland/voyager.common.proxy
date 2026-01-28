namespace Voyager.Common.Proxy.Abstractions
{
    /// <summary>
    /// Specifies that a method should use HTTP PATCH.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PATCH is used for partial updates to resources.
    /// Complex type parameters are sent as JSON in the request body.
    /// </para>
    /// <para>
    /// This attribute must be explicitly specified - there is no convention-based detection for PATCH.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public interface IUserService
    /// {
    ///     [HttpPatch("/users/{id}")]
    ///     Task&lt;Result&lt;User&gt;&gt; PatchUserAsync(int id, PatchUserRequest request);
    /// }
    /// </code>
    /// </example>
    public sealed class HttpPatchAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPatchAttribute"/> class.
        /// </summary>
        /// <param name="template">
        /// The route template. If null, the route is derived from the method name.
        /// </param>
        public HttpPatchAttribute(string? template = null)
            : base(HttpMethod.Patch, template)
        {
        }
    }
}
