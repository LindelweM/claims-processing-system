using Claims.Contracts.Integration;

namespace Claims.Integration.Payments;

/// <summary>
/// Instructs the payment provider to disburse approved claims.
/// </summary>
public interface IPaymentClient
{
    /// <summary>Instructs a payment against an approved claim.</summary>
    /// <param name="request">Amount and account to pay.</param>
    /// <param name="cancellationToken">Token to cancel the call.</param>
    /// <returns>
    /// The provider's acknowledgement. A refused instruction returns a rejected result rather
    /// than throwing; only transport and protocol faults throw.
    /// </returns>
    Task<PaymentInstructionResult> RequestPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default);
}
