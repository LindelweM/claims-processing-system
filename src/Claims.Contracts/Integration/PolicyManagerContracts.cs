using Claims.Contracts.Enums;

namespace Claims.Contracts.Integration;

/// <summary>
/// Request to the policy manager to confirm a policy covers the incident being claimed.
/// </summary>
public sealed record PolicyVerificationRequest
{
    /// <summary>Claim the verification is being performed for.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>Number of the policy being claimed against.</summary>
    public required string PolicyNumber { get; init; }

    /// <summary>Identifier of the client, used to confirm ownership of the policy.</summary>
    public required string ClientId { get; init; }

    /// <summary>Date of the incident, checked against the policy's period of cover.</summary>
    public required DateOnly IncidentDate { get; init; }

    /// <summary>Category of cover the claim is being made under.</summary>
    public required ClaimType ClaimType { get; init; }

    /// <summary>Amount being claimed, checked against the remaining benefit, in <see cref="Currency"/>.</summary>
    public required decimal ClaimAmount { get; init; }

    /// <summary>ISO 4217 currency code for <see cref="ClaimAmount"/>.</summary>
    public required string Currency { get; init; }
}

/// <summary>
/// Policy manager's verdict on whether a policy covers the claimed incident.
/// </summary>
public sealed record PolicyVerificationResponse
{
    /// <summary>True when the policy manager approved cover for this claim.</summary>
    public required bool IsValid { get; init; }

    /// <summary>True when the policy was in force on the incident date.</summary>
    public bool PolicyActive { get; init; }

    /// <summary>True when the policy's benefits extend to the claim type being made.</summary>
    public bool BenefitCovered { get; init; }

    /// <summary>Amount the policy manager approved for this claim, in <see cref="Currency"/>.</summary>
    public decimal ApprovedAmount { get; init; }

    /// <summary>ISO 4217 currency code for <see cref="ApprovedAmount"/>.</summary>
    public string? Currency { get; init; }

    /// <summary>
    /// Why cover was declined, for surfacing to an assessor.
    /// Set when <see cref="IsValid"/> is false.
    /// </summary>
    public string? FailureReason { get; init; }

    /// <summary>Creates a verdict approving cover for the claim.</summary>
    /// <param name="approvedAmount">Amount the policy manager approved.</param>
    /// <param name="currency">ISO 4217 currency code for <paramref name="approvedAmount"/>.</param>
    public static PolicyVerificationResponse Approved(decimal approvedAmount, string currency) => new()
    {
        IsValid = true,
        PolicyActive = true,
        BenefitCovered = true,
        ApprovedAmount = approvedAmount,
        Currency = currency
    };

    /// <summary>Creates a verdict declining cover for the claim.</summary>
    /// <param name="reason">Why cover was declined.</param>
    /// <param name="policyActive">
    /// Whether the policy was in force on the incident date. True when the decline is for some
    /// other reason, which tells an assessor the policy itself is not the problem.
    /// </param>
    /// <param name="benefitCovered">
    /// Whether the policy's benefits extend to the claim type. True when the decline is for some
    /// other reason.
    /// </param>
    public static PolicyVerificationResponse Declined(
        string reason,
        bool policyActive = false,
        bool benefitCovered = false) => new()
    {
        IsValid = false,
        PolicyActive = policyActive,
        BenefitCovered = benefitCovered,
        ApprovedAmount = 0m,
        Currency = null,
        FailureReason = reason
    };
}
