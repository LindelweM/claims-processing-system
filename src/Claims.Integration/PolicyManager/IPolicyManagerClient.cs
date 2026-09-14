using Claims.Contracts.Integration;

namespace Claims.Integration.PolicyManager;

/// <summary>
/// Calls the policy manager to confirm a policy covers the incident being claimed.
/// </summary>
public interface IPolicyManagerClient
{
    /// <summary>Asks the policy manager whether the claim is covered.</summary>
    /// <param name="request">Policy and incident details to verify.</param>
    /// <param name="cancellationToken">Token to cancel the call.</param>
    /// <returns>
    /// The policy manager's verdict. A declined claim returns a declined response rather than
    /// throwing; only transport and protocol faults throw.
    /// </returns>
    Task<PolicyVerificationResponse> VerifyAsync(
        PolicyVerificationRequest request,
        CancellationToken cancellationToken = default);
}
