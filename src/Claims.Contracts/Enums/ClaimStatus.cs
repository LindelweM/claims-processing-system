namespace Claims.Contracts.Enums;

/// <summary>
/// Lifecycle state of a claim as it moves through intake, assessment and settlement.
/// </summary>
public enum ClaimStatus
{
    Received = 0,
    Validating = 1,
    ClientValidated = 2,
    PolicyValidated = 3,
    Approved = 4,
    Rejected = 5,
    PaymentRequested = 6,
    Paid = 7,
    PaymentFailed = 8,
    Completed = 9,
    Failed = 10
}
