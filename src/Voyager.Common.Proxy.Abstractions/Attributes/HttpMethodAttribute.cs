namespace Voyager.Common.Proxy.Abstractions
{
    using System;

    /// <summary>
    /// Base attribute for specifying HTTP method and route template for a service method.
    /// Use derived attributes (<see cref="HttpGetAttribute"/>, <see cref="HttpPostAttribute"/>, etc.)
    /// for cleaner syntax.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute is optional. When not specified, the proxy uses convention-based routing:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Methods starting with Get*, Find*, List* use HTTP GET</description></item>
    /// <item><description>Methods starting with Create*, Add* use HTTP POST</description></item>
    /// <item><description>Methods starting with Update* use HTTP PUT</description></item>
    /// <item><description>Methods starting with Delete*, Remove* use HTTP DELETE</description></item>
    /// <item><description>Other methods default to HTTP POST</description></item>
    /// </list>
    /// <para>
    /// Route templates support parameter placeholders using curly braces: <c>/users/{id}</c>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public interface IUserService
    /// {
    ///     [HttpMethod(HttpMethod.Get, "/users/{id}")]
    ///     Task&lt;Result&lt;User&gt;&gt; GetUserAsync(int id);
    ///
    ///     // Or use the derived attribute for cleaner syntax:
    ///     [HttpGet("/users/{id}")]
    ///     Task&lt;Result&lt;User&gt;&gt; GetUserAsync(int id);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class HttpMethodAttribute : Attribute
    {
        /// <summary>
        /// Gets the HTTP method for this endpoint.
        /// </summary>
        public HttpMethod Method { get; }

        /// <summary>
        /// Gets the route template for this endpoint.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The template can contain parameter placeholders in curly braces.
        /// Parameter names are matched case-insensitively to method parameters.
        /// </para>
        /// <para>
        /// If null or empty, the route is derived from the method name using kebab-case convention.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Template: "/users/{id}" matches parameter "int id"
        /// // Template: "/users/{userId}/orders" matches parameter "int userId"
        /// </code>
        /// </example>
        public string? Template { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpMethodAttribute"/> class.
        /// </summary>
        /// <param name="method">The HTTP method to use.</param>
        /// <param name="template">
        /// The route template. If null, the route is derived from the method name.
        /// </param>
        public HttpMethodAttribute(HttpMethod method, string? template = null)
        {
            Method = method;
            Template = template;
        }
    }
}
