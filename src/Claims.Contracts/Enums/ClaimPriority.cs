namespace Claims.Contracts.Enums;

/// <summary>
/// Processing urgency assigned to a claim, used to order work queues.
/// </summary>
public enum ClaimPriority
{
    Standard = 0,
    High = 1,
    Critical = 2
}
