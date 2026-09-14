namespace Claims.Contracts.Enums;

/// <summary>
/// State of the disbursement raised against an approved claim.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
   
}
