namespace Voyager.Common.Proxy.Abstractions.Validation
{
    using Voyager.Common.Results;

    /// <summary>
    /// Interface for request models that support validation returning Result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implement this interface on request models to enable automatic validation
    /// before the service method is invoked. When a method or interface is marked
    /// with <see cref="ValidateRequestAttribute"/>, the proxy will call <see cref="IsValid"/>
    /// on all parameters that implement this interface.
    /// </para>
    /// <para>
    /// This is the recommended approach for new code as it provides compile-time
    /// type checking and full IDE support.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public class CreatePaymentRequest : IValidatableRequest
    /// {
    ///     public decimal Amount { get; set; }
    ///     public string Currency { get; set; }
    ///
    ///     public Result IsValid()
    ///     {
    ///         if (Amount &lt;= 0)
    ///             return Result.Failure(Error.ValidationError("Amount must be positive"));
    ///         if (string.IsNullOrEmpty(Currency))
    ///             return Result.Failure(Error.ValidationError("Currency is required"));
    ///         if (Currency.Length != 3)
    ///             return Result.Failure(Error.ValidationError("Currency must be 3 characters"));
    ///         return Result.Success();
    ///     }
    /// }
    /// </code>
    /// </example>
    public interface IValidatableRequest
    {
        /// <summary>
        /// Validates the request and returns a Result indicating success or validation errors.
        /// </summary>
        /// <returns>
        /// A <see cref="Result"/> that is successful if the request is valid,
        /// or contains an error if validation fails.
        /// </returns>
        Result IsValid();
    }
}
