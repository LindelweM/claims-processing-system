namespace Claims.Functions.Models;

/// <summary>
/// What the policy verification activity needs to confirm cover: the claim to verify, and the
/// client the registry matched it to. The activity loads the rest from the claim itself.
/// </summary>
public sealed class ValidatePolicyActivityInput
{
    /// <summary>Identifier of the claim whose policy is being verified.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>
    /// Client registry's identifier for the claimant, carried from the preceding validation
    /// step so the policy manager can confirm the claimant's link to the policy.
    /// </summary>
    public required string ClientId { get; init; }
}
