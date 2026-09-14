using Claims.Contracts.Enums;

namespace Claims.Contracts.Intake;

/// <summary>
/// Acknowledgement returned once a claim submission has been accepted for processing.
/// </summary>
public sealed record ClaimSubmissionResponse
{
    /// <summary>System-assigned identifier for the claim.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>Human-readable reference quoted to the claimant.</summary>
    public required string ClaimReference { get; init; }

    /// <summary>State the claim was placed in on acceptance.</summary>
    public required ClaimStatus Status { get; init; }

    /// <summary>Processing urgency assigned to the claim during intake.</summary>
    public required ClaimPriority Priority { get; init; }

    /// <summary>Date by which the claim is expected to be assessed.</summary>
    public required DateTimeOffset Deadline { get; init; }

    /// <summary>Optional note to the submitter about the outcome of intake.</summary>
    public string? Message { get; init; }
}
