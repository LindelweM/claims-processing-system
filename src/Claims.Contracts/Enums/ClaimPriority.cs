namespace Claims.Contracts.Enums;

/// <summary>
/// Processing urgency assigned to a claim, used to order work queues.
/// </summary>
public enum ClaimPriority
{
    Unknown = 0,
    Low = 1,
    Normal = 2,
    High = 3,
    Urgent = 4
}
