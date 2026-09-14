namespace Claims.Contracts.Enums;

/// <summary>
/// Category of cover a claim is made against.
/// </summary>
public enum ClaimType
{
    Unknown = 0,
    Medical = 1,
    Motor = 2,
    Property = 3,
    Travel = 4,
    Liability = 5,
    Life = 6,
    Disability = 7
}
