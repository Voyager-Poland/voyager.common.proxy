namespace Voyager.Common.Proxy.Abstractions
{
    /// <summary>
    /// Specifies that a method should use HTTP DELETE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DELETE is used for removing resources.
    /// Parameters are typically sent as route or query string parameters.
    /// </para>
    /// <para>
    /// This attribute is optional. Methods starting with Delete* or Remove* automatically use DELETE.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public interface IUserService
    /// {
    ///     // Explicit route template
    ///     [HttpDelete("/users/{id}")]
    ///     Task&lt;Result&gt; DeleteUserAsync(int id);
    ///
    ///     // Convention-based (no attribute needed)
    ///     Task&lt;Result&gt; DeleteUserAsync(int id);
    ///     // Results in: DELETE /user-service/delete-user?id=123
    ///
    ///     Task&lt;Result&gt; RemoveUserFromGroupAsync(int userId, int groupId);
    ///     // Results in: DELETE /user-service/remove-user-from-group?userId=123&amp;groupId=456
    /// }
    /// </code>
    /// </example>
    public sealed class HttpDeleteAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpDeleteAttribute"/> class.
        /// </summary>
        /// <param name="template">
        /// The route template. If null, the route is derived from the method name.
        /// </param>
        public HttpDeleteAttribute(string? template = null)
            : base(HttpMethod.Delete, template)
        {
        }
    }
}
