using Claims.Contracts.Integration;

namespace Claims.Functions.Models;

/// <summary>
/// What the settlement activity needs to close out a payment: the claim it belongs to, and the
/// outcome the provider reported. The notification is carried in full rather than reduced to a
/// status, so the reference and failure reason are recorded on the claim's audit trail.
/// </summary>
public sealed class PaymentCompletionActivityInput
{
    /// <summary>Identifier of the claim the payment settles.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>
    /// What the payment provider reported, as received by the callback endpoint and passed
    /// through the orchestration.
    /// </summary>
    public required PaymentCompletionNotification Notification { get; init; }
}
