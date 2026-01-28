namespace Voyager.Common.Proxy.Abstractions
{
    using System;

    /// <summary>
    /// Specifies the base route prefix for all methods in a service interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute is optional. When not specified, the base route is derived from
    /// the interface name using kebab-case convention (e.g., IUserService becomes "user-service").
    /// </para>
    /// <para>
    /// The route prefix is prepended to all method routes in the interface.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Custom base route
    /// [ServiceRoute("api/v2/users")]
    /// public interface IUserService
    /// {
    ///     [HttpGet("{id}")]
    ///     Task&lt;Result&lt;User&gt;&gt; GetUserAsync(int id);
    ///     // Results in: GET /api/v2/users/{id}
    ///
    ///     Task&lt;Result&lt;User&gt;&gt; CreateUserAsync(CreateUserRequest request);
    ///     // Results in: POST /api/v2/users/create-user
    /// }
    ///
    /// // Convention-based (no attribute)
    /// public interface IOrderService
    /// {
    ///     Task&lt;Result&lt;Order&gt;&gt; GetOrderAsync(int id);
    ///     // Results in: GET /order-service/get-order?id=123
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public sealed class ServiceRouteAttribute : Attribute
    {
        /// <summary>
        /// Gets the base route prefix for the service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The prefix should not start or end with a slash.
        /// Leading and trailing slashes are automatically handled by the proxy.
        /// </para>
        /// </remarks>
        public string Prefix { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRouteAttribute"/> class.
        /// </summary>
        /// <param name="prefix">
        /// The base route prefix for all methods in the service.
        /// Should not start or end with a slash.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="prefix"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="prefix"/> is empty or whitespace.
        /// </exception>
        public ServiceRouteAttribute(string prefix)
        {
            if (prefix is null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException("Route prefix cannot be empty or whitespace.", nameof(prefix));
            }

            Prefix = prefix.Trim('/');
        }
    }
}
