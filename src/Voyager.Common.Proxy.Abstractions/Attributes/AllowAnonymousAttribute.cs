namespace Voyager.Common.Proxy.Abstractions
{
    using System;

    /// <summary>
    /// Specifies that the method allows anonymous access, overriding interface-level
    /// <see cref="RequireAuthorizationAttribute"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this attribute on methods that should be publicly accessible even when
    /// the interface has a <see cref="RequireAuthorizationAttribute"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [RequireAuthorization]
    /// public interface IProductService
    /// {
    ///     // Requires authorization (inherited from interface)
    ///     Task&lt;Result&lt;Product&gt;&gt; CreateProductAsync(CreateProductRequest request);
    ///
    ///     // Publicly accessible
    ///     [AllowAnonymous]
    ///     Task&lt;Result&lt;Product&gt;&gt; GetProductAsync(int id);
    ///
    ///     // Publicly accessible
    ///     [AllowAnonymous]
    ///     Task&lt;Result&lt;IEnumerable&lt;Product&gt;&gt;&gt; SearchProductsAsync(string query);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AllowAnonymousAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AllowAnonymousAttribute"/> class.
        /// </summary>
        public AllowAnonymousAttribute()
        {
        }
    }
}
