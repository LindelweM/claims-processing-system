namespace Claims.Functions.Models;

/// <summary>
/// What the rejection activity needs to end a claim: the claim to reject, and why. The reason
/// is recorded on the claim and its audit trail, so it must be fit to show a claimant.
/// </summary>
public sealed class RejectClaimsActivityInput
{
    /// <summary>Identifier of the claim being rejected.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>
    /// Why the claim was refused, as reported by whichever step turned it down.
    /// </summary>
    public required string Reason { get; init; }
}
