namespace Claims.Contracts.Enums;

/// <summary>
/// Lifecycle state of a claim as it moves through intake, assessment and settlement.
/// </summary>
public enum ClaimStatus
{
    Unknown = 0,
    Draft = 1,
    Submitted = 2,
    Received = 3,
    Validating = 4,
    UnderReview = 5,
    PendingInformation = 6,
    Approved = 7,
    PartiallyApproved = 8,
    Rejected = 9,
    Settled = 10,
    Closed = 11,
    Cancelled = 12,
    ClientValidated = 13,
    PolicyValidated = 14,
    PaymentRequested = 15,
    Paid = 16,
    PaymentFailed = 17,
    Completed = 18,
    Failed = 19
}
