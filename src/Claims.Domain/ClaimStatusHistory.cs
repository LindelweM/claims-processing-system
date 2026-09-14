using Claims.Contracts.Enums;

namespace Claims.Domain;

/// <summary>
/// A single recorded event in a claim's lifecycle, forming the claim's audit trail. An entry
/// is written both when the claim changes state and when a milestone is recorded within one.
/// </summary>
public sealed class ClaimStatusHistory
{
    /// <summary>Identifier for this history entry.</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Claim this entry belongs to.</summary>
    public Guid ClaimId { get; private set; }

    /// <summary>State the claim was in as at this entry.</summary>
    public ClaimStatus Status { get; private set; }

    /// <summary>When the recorded event happened.</summary>
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>What happened, for surfacing to an assessor or the claimant.</summary>
    public string? Detail { get; private set; }

    private ClaimStatusHistory()
    {
    }

    /// <summary>Records an event against a claim, at the state the claim was in.</summary>
    /// <param name="claimId">Claim the entry belongs to.</param>
    /// <param name="status">State the claim was in as at this entry.</param>
    /// <param name="occurredAt">When the recorded event happened.</param>
    /// <param name="detail">What happened.</param>
    /// <returns>The entry to append to the claim's audit trail.</returns>
    public static ClaimStatusHistory Record(
        Guid claimId,
        ClaimStatus status,
        DateTimeOffset occurredAt,
        string? detail = null) => new()
        {
            ClaimId = claimId,
            Status = status,
            OccurredAt = occurredAt,
            Detail = detail
        };
}
