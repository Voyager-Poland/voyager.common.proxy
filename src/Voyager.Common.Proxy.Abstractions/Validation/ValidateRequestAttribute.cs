namespace Voyager.Common.Proxy.Abstractions.Validation
{
    using System;

    /// <summary>
    /// Indicates that request parameters should be validated before processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When applied to a method or interface, the proxy will automatically validate
    /// all request parameters that implement <see cref="IValidatableRequest"/>,
    /// <see cref="IValidatableRequestBool"/>, or have methods marked with
    /// <see cref="ValidationMethodAttribute"/>.
    /// </para>
    /// <para>
    /// <b>Server-side validation</b> (default): Validation occurs after the request
    /// is deserialized but before the service method is invoked. If validation fails,
    /// an HTTP 400 response is returned with the validation error message.
    /// </para>
    /// <para>
    /// <b>Client-side validation</b> (when <see cref="ClientSide"/> is true): Validation
    /// is performed ADDITIONALLY on the client before the HTTP call is made. This is an
    /// optimization to avoid network traffic for invalid requests. Server-side validation
    /// ALWAYS occurs regardless of this setting for security reasons.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Validate all methods in the interface (server-side only)
    /// [ValidateRequest]
    /// public interface IPaymentService
    /// {
    ///     Task&lt;Result&lt;PaymentResponse&gt;&gt; CreatePaymentAsync(CreatePaymentRequest request);
    /// }
    ///
    /// // Validate specific method with client-side optimization
    /// public interface IOrderService
    /// {
    ///     [ValidateRequest(ClientSide = true)]
    ///     Task&lt;Result&gt; PlaceOrderAsync(PlaceOrderRequest request);
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
    public sealed class ValidateRequestAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets a value indicating whether validation should be performed
        /// additionally on the client-side before the HTTP call.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When set to true, validation is performed on the client before making
        /// the HTTP request. If validation fails, a failure Result is returned
        /// immediately without making the network call.
        /// </para>
        /// <para>
        /// <b>Important:</b> Server-side validation ALWAYS happens regardless of this
        /// setting. Client-side validation is an optimization to reduce network traffic,
        /// not a replacement for server-side validation. This ensures:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Malicious clients cannot bypass validation</description></item>
        ///   <item><description>Outdated client versions don't compromise security</description></item>
        ///   <item><description>Data consistency is guaranteed on the server</description></item>
        /// </list>
        /// </remarks>
        /// <value>
        /// <c>true</c> to enable client-side validation in addition to server-side;
        /// <c>false</c> (default) for server-side validation only.
        /// </value>
        public bool ClientSide { get; set; } = false;
    }
}
