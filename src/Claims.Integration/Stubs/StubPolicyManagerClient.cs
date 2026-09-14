using Claims.Contracts.Enums;
using Claims.Contracts.Integration;
using Claims.Integration.PolicyManager;
using Microsoft.Extensions.Logging;

namespace Claims.Integration.Stubs;

/// <summary>
/// In-memory stand-in for the policy manager, for local runs and tests.
/// </summary>
/// <remarks>
/// Approves every claim for the full amount requested, except where the request is shaped to
/// force a decline: a policy number that is blank or starts with INVALID, one containing
/// LAPSED, an unknown claim type, or a non-positive amount.
/// </remarks>
public sealed class StubPolicyManagerClient : IPolicyManagerClient
{
    private readonly ILogger<StubPolicyManagerClient> logger;

    /// <summary>Creates the stub policy manager.</summary>
    /// <param name="logger">Log recording that a stub answered.</param>
    public StubPolicyManagerClient(ILogger<StubPolicyManagerClient> logger) => this.logger = logger;

    /// <inheritdoc />
    public Task<PolicyVerificationResponse> VerifyAsync(
        PolicyVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Stub policy manager validating claim {ClaimId} on policy {PolicyNumber}",
            request.ClaimId,
            request.PolicyNumber);

        if (string.IsNullOrWhiteSpace(request.PolicyNumber) ||
            request.PolicyNumber.StartsWith("INVALID", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(PolicyVerificationResponse.Declined(
                "Policy number is invalid.",
                policyActive: false));
        }

        if (request.PolicyNumber.Contains("LAPSED", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(PolicyVerificationResponse.Declined(
                "Policy has lapsed.",
                policyActive: false,
                benefitCovered: true));
        }

        if (request.ClaimType == ClaimType.Unknown)
        {
            return Task.FromResult(PolicyVerificationResponse.Declined(
                "Claim type is unknown.",
                policyActive: true,
                benefitCovered: false));
        }

        if (request.ClaimAmount <= 0)
        {
            return Task.FromResult(PolicyVerificationResponse.Declined(
                "Claim amount must be greater than zero.",
                policyActive: true,
                benefitCovered: true));
        }

        return Task.FromResult(PolicyVerificationResponse.Approved(request.ClaimAmount, request.Currency));
    }
}
