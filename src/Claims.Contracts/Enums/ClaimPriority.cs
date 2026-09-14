namespace Claims.Contracts.Enums;

/// <summary>
/// Processing urgency assigned to a claim, used to order work queues.
/// </summary>
public enum ClaimPriority
{
    Unknown = 0,
    Standard = 1,
    High = 2,
    Critical = 3
}
