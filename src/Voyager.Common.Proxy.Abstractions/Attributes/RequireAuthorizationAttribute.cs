namespace Voyager.Common.Proxy.Abstractions
{
    using System;

    /// <summary>
    /// Specifies that the service interface or method requires authorization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When applied to an interface, all methods in the interface require authorization.
    /// When applied to a method, only that method requires authorization.
    /// </para>
    /// <para>
    /// This attribute works with ASP.NET Core's authorization system. You can specify
    /// policy names, roles, or authentication schemes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Require authorization for all methods
    /// [RequireAuthorization]
    /// public interface IUserService
    /// {
    ///     Task&lt;Result&lt;User&gt;&gt; GetUserAsync(int id);
    ///     Task&lt;Result&lt;User&gt;&gt; CreateUserAsync(CreateUserRequest request);
    /// }
    ///
    /// // Require specific policy
    /// [RequireAuthorization("AdminPolicy")]
    /// public interface IAdminService
    /// {
    ///     Task&lt;Result&gt; DeleteUserAsync(int id);
    /// }
    ///
    /// // Mixed authorization (interface + method level)
    /// [RequireAuthorization]
    /// public interface IOrderService
    /// {
    ///     Task&lt;Result&lt;Order&gt;&gt; GetOrderAsync(int id);
    ///
    ///     [RequireAuthorization("AdminPolicy")]
    ///     Task&lt;Result&gt; CancelOrderAsync(int id);
    ///
    ///     [AllowAnonymous]
    ///     Task&lt;Result&lt;OrderStatus&gt;&gt; GetOrderStatusAsync(int id);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class RequireAuthorizationAttribute : Attribute
    {
        /// <summary>
        /// Gets the authorization policy name, if specified.
        /// </summary>
        public string? Policy { get; }

        /// <summary>
        /// Gets or sets the roles that are allowed to access the resource.
        /// </summary>
        /// <remarks>
        /// Multiple roles can be specified as a comma-separated list.
        /// </remarks>
        public string? Roles { get; set; }

        /// <summary>
        /// Gets or sets the authentication schemes that are required.
        /// </summary>
        /// <remarks>
        /// Multiple schemes can be specified as a comma-separated list.
        /// </remarks>
        public string? AuthenticationSchemes { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequireAuthorizationAttribute"/> class
        /// with default authorization (any authenticated user).
        /// </summary>
        public RequireAuthorizationAttribute()
        {
            Policy = null;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RequireAuthorizationAttribute"/> class
        /// with a specific authorization policy.
        /// </summary>
        /// <param name="policy">The name of the authorization policy to require.</param>
        public RequireAuthorizationAttribute(string policy)
        {
            Policy = policy;
        }
    }
}
