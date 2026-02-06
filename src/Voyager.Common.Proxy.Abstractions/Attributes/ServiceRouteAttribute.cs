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
    /// // No route prefix (external API without common prefix)
    /// [ServiceRoute(ServiceRouteAttribute.NoPrefix)]
    /// public interface IExternalApiService
    /// {
    ///     [HttpPost("NewOrder")]
    ///     Task&lt;Result&lt;Order&gt;&gt; NewOrder(Order order);
    ///     // Results in: POST /NewOrder
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
        /// Use this constant to explicitly indicate that the service has no route prefix.
        /// Methods will be mapped directly under the root path.
        /// </summary>
        /// <example>
        /// <code>
        /// [ServiceRoute(ServiceRouteAttribute.NoPrefix)]
        /// public interface IExternalApiService
        /// {
        ///     [HttpPost("NewOrder")]
        ///     Task&lt;Result&lt;Order&gt;&gt; NewOrder(Order order);
        ///     // Results in: POST /NewOrder
        /// }
        /// </code>
        /// </example>
        public const string NoPrefix = "";

        /// <summary>
        /// Gets the base route prefix for the service.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The prefix should not start or end with a slash.
        /// Leading and trailing slashes are automatically handled by the proxy.
        /// An empty string means no prefix — methods are mapped directly under the root path.
        /// </para>
        /// </remarks>
        public string Prefix { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServiceRouteAttribute"/> class.
        /// </summary>
        /// <param name="prefix">
        /// The base route prefix for all methods in the service.
        /// Should not start or end with a slash.
        /// Use <see cref="NoPrefix"/> or empty string for services without a route prefix.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="prefix"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="prefix"/> contains only whitespace characters.
        /// </exception>
        public ServiceRouteAttribute(string prefix)
        {
            if (prefix is null)
            {
                throw new ArgumentNullException(nameof(prefix));
            }

            if (prefix.Length > 0 && string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException(
                    "Route prefix cannot contain only whitespace. Use ServiceRouteAttribute.NoPrefix for no prefix.",
                    nameof(prefix));
            }

            Prefix = prefix.Trim('/');
        }
    }
}
