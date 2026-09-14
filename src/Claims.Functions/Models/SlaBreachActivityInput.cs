namespace Claims.Functions.Models;

/// <summary>
/// What the SLA breach activity needs to flag a claim as overdue: the claim, and a note for
/// its audit trail. Flagging a breach does not change the claim's state, so the claim stays
/// in whichever queue it is already sitting in.
/// </summary>
public sealed class SlaBreachActivityInput
{
    /// <summary>Identifier of the claim that missed its assessment deadline.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>
    /// Note recorded against the claim explaining the breach, such as the deadline that passed.
    /// </summary>
    public required string Detail { get; init; }
}
