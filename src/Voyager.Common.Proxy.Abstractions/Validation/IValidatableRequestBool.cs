namespace Voyager.Common.Proxy.Abstractions.Validation
{
    /// <summary>
    /// Interface for request models that support simple boolean validation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this interface when you prefer simple boolean validation with a separate
    /// error message property. For more complex validation with multiple potential
    /// errors, consider using <see cref="IValidatableRequest"/> instead.
    /// </para>
    /// <para>
    /// When a method or interface is marked with <see cref="ValidateRequestAttribute"/>,
    /// the proxy will call <see cref="IsValid"/> on all parameters that implement
    /// this interface. If validation fails, <see cref="ValidationErrorMessage"/>
    /// is used as the error message.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class SimpleRequest : IValidatableRequestBool
    /// {
    ///     public int Id { get; set; }
    ///
    ///     public bool IsValid() => Id > 0;
    ///
    ///     public string? ValidationErrorMessage => Id &lt;= 0 ? "Id must be positive" : null;
    /// }
    /// </code>
    /// </example>
    public interface IValidatableRequestBool
    {
        /// <summary>
        /// Validates the request and returns true if valid.
        /// </summary>
        /// <returns>True if the request is valid; otherwise, false.</returns>
        bool IsValid();

        /// <summary>
        /// Gets the validation error message when <see cref="IsValid"/> returns false.
        /// </summary>
        /// <remarks>
        /// This property should return a meaningful error message when validation fails,
        /// or null when the request is valid.
        /// </remarks>
        string? ValidationErrorMessage { get; }
    }
}
