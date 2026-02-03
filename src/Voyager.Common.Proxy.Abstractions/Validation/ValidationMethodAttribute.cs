namespace Voyager.Common.Proxy.Abstractions.Validation
{
    using System;

    /// <summary>
    /// Marks a method as the validation method for the request model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this attribute when you cannot or prefer not to implement
    /// <see cref="IValidatableRequest"/> or <see cref="IValidatableRequestBool"/>.
    /// This is particularly useful for integrating with existing models that
    /// already have validation methods.
    /// </para>
    /// <para>
    /// The method must:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Be a public instance method</description></item>
    ///   <item><description>Take no parameters</description></item>
    ///   <item><description>Return either <c>Result</c> or <c>bool</c></description></item>
    /// </list>
    /// <para>
    /// For methods returning <c>bool</c>, use <see cref="ErrorMessage"/> to specify
    /// the error message when validation fails.
    /// </para>
    /// <para>
    /// <b>Performance note:</b> Using interfaces (<see cref="IValidatableRequest"/>,
    /// <see cref="IValidatableRequestBool"/>) is faster than using this attribute
    /// because interfaces don't require reflection at runtime.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Existing model - just add the attribute
    /// public class LegacyPaymentRequest
    /// {
    ///     public decimal Amount { get; set; }
    ///     public string Currency { get; set; }
    ///
    ///     [ValidationMethod]
    ///     public Result Validate()  // Method name can be anything
    ///     {
    ///         return Result.Success()
    ///             .Ensure(() => Amount > 0, Error.ValidationError("Amount must be positive"))
    ///             .Ensure(() => !string.IsNullOrEmpty(Currency), Error.ValidationError("Currency is required"));
    ///     }
    /// }
    ///
    /// // Boolean validation with custom error message
    /// public class SimpleBookingRequest
    /// {
    ///     public int BookingId { get; set; }
    ///
    ///     [ValidationMethod(ErrorMessage = "BookingId must be positive")]
    ///     public bool CheckIsValid() => BookingId > 0;
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ValidationMethodAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the error message to use when validation fails.
        /// </summary>
        /// <remarks>
        /// This property is only used when the validation method returns <c>bool</c>.
        /// For methods returning <c>Result</c>, the error message from the Result is used instead.
        /// If not specified and the method returns <c>bool</c>, the default message
        /// "Request validation failed" is used.
        /// </remarks>
        public string? ErrorMessage { get; set; }
    }
}
