using Claims.Contracts.Enums;

namespace Claims.Contracts.Status;

/// <summary>
/// Response returned when querying the status of a claim.
/// </summary>

public sealed record ClaimStatusResponse
{
    /// <summary>System-assigned identifier for the claim.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>Human-readable reference quoted to the claimant.</summary>
    public required string ClaimReference { get; init; }

     public required ClaimType Type { get; init; }

    /// <summary>State the claim is currently in.</summary>
    public required ClaimStatus Status { get; init; }

    /// <summary>Processing urgency assigned to the claim during intake.</summary>
    public required ClaimPriority Priority { get; init; }
    public required DateTimeOffset SubmittedDate { get; init; }

    /// <summary>Date by which the claim is expected to be assessed.</summary>
    public required DateTimeOffset Deadline { get; init; }

    public required bool SlaBreached { get; init; }

    public decimal? ApprovedAmount { get; init; }
    public string? Currency { get; init; }
    public string? PaymentReference { get; init; }
    public PaymentStatus? PaymentStatus { get; init; }
    public IReadOnlyList<ClaimStatusHistoryEntry> History { get; init; } = Array.Empty<ClaimStatusHistoryEntry>();

}

public sealed record ClaimStatusHistoryEntry
{
    public required ClaimStatus Status { get; init; }
    public required DateTimeOffset OccuredAt { get; init; }
    public string? Detail { get; init; }
}

