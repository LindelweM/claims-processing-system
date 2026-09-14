using Claims.Contracts.Enums;

namespace Claims.Functions.Models;

/// <summary>
/// What the claim orchestration is started with. Carries only the values the orchestration
/// itself decides on, so activities load the claim's own details from the store rather than
/// working off a copy that could be stale by the time they run.
/// </summary>
public sealed class ClaimProcessingInput
{
    /// <summary>Identifier of the claim being processed.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>Urgency the claim is worked at, which sets its assessment deadline.</summary>
    public required ClaimPriority Priority { get; init; }

    /// <summary>Date by which the claim must be assessed before its SLA is breached.</summary>
    public required DateTimeOffset SlaDeadline { get; init; }

    /// <summary>
    /// How long to wait for the payment provider's settlement callback before giving up on it.
    /// A payment still unsettled after this needs a human to reconcile it.
    /// </summary>
    public TimeSpan PaymentWaitTimeout { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Address the payment provider calls back on once the payment settles.</summary>
    public required string PaymentCallbackUrl { get; init; }
}
