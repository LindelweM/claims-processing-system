namespace Claims.Functions.Models;

/// <summary>
/// What the payment activity needs to instruct a disbursement: the claim to pay, and where the
/// provider should report settlement. The amount and account are loaded from the claim itself,
/// so the figure paid is always the one the policy manager authorised.
/// </summary>
public sealed class InitiatePaymentActivityInput
{
    /// <summary>Identifier of the approved claim being paid.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>
    /// Address the payment provider calls back on once the payment settles, which wakes the
    /// waiting orchestration.
    /// </summary>
    public required string CallbackUrl { get; init; }
}
