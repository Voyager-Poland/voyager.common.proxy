namespace Voyager.Common.Proxy.Abstractions
{
    /// <summary>
    /// Specifies that a method should use HTTP POST.
    /// </summary>
    /// <remarks>
    /// <para>
    /// POST is used for creating resources or executing actions.
    /// Complex type parameters are sent as JSON in the request body.
    /// </para>
    /// <para>
    /// This attribute is optional. Methods starting with Create* or Add* automatically use POST.
    /// Methods that don't match any convention also default to POST.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public interface IUserService
    /// {
    ///     // Explicit route template with body
    ///     [HttpPost("/users")]
    ///     Task&lt;Result&lt;User&gt;&gt; CreateUserAsync(CreateUserRequest request);
    ///
    ///     // Mixed: route parameter + body
    ///     [HttpPost("/users/{userId}/orders")]
    ///     Task&lt;Result&lt;Order&gt;&gt; CreateOrderAsync(int userId, CreateOrderRequest request);
    ///
    ///     // Convention-based (no attribute needed)
    ///     Task&lt;Result&lt;User&gt;&gt; CreateUserAsync(CreateUserRequest request);
    ///     // Results in: POST /user-service/create-user
    /// }
    /// </code>
    /// </example>
    public sealed class HttpPostAttribute : HttpMethodAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpPostAttribute"/> class.
        /// </summary>
        /// <param name="template">
        /// The route template. If null, the route is derived from the method name.
        /// </param>
        public HttpPostAttribute(string? template = null)
            : base(HttpMethod.Post, template)
        {
        }
    }
}
