using System.ComponentModel.DataAnnotations;
using Claims.Contracts.Enums;

namespace Claims.Contracts.Intake;

/// <summary>
/// Payload submitted by an external channel to lodge a new claim.
/// </summary>
public sealed record ClaimSubmissionRequest
{
    /// <summary>The submission channel, such as the web form, mobile app or broker feed.</summary>
    [Required]
    public string Channel { get; init; } = "WebForm";

    /// <summary>Optional reference supplied by the channel, echoed back for reconciliation.</summary>
    public string? ChannelReference { get; init; }

    /// <summary>Category of cover being claimed.</summary>
    [Required]
    public required ClaimType ClaimType { get; init; }

    /// <summary>Party lodging the claim.</summary>
    [Required]
    public required ClaimantInfo Claimant { get; init; }

    /// <summary>Policy the claim is made against.</summary>
    [Required]
    public required PolicyInfo Policy { get; init; }

    /// <summary>The incident or loss giving rise to the claim.</summary>
    [Required]
    public required IncidentInfo Incident { get; init; }

    /// <summary>Account into which an approved claim will be settled.</summary>
    [Required]
    public required BankingDetails BankingDetails { get; init; }
}

/// <summary>
/// Identifying and contact details for the party lodging a claim.
/// </summary>
public sealed record ClaimantInfo
{
    /// <summary>Claimant's given name.</summary>
    [Required]
    public required string FirstName { get; init; }

    /// <summary>Claimant's family name.</summary>
    [Required]
    public required string LastName { get; init; }

    /// <summary>National or member identifier, where the channel captures one.</summary>
    public string? IdentityNumber { get; init; }

    /// <summary>Email address used to correspond with the claimant.</summary>
    [EmailAddress]
    public string? EmailAddress { get; init; }

    /// <summary>Contact telephone number for the claimant.</summary>
    [Phone]
    public string? PhoneNumber { get; init; }

    /// <summary>How the claimant relates to the policyholder, where they are not the same person.</summary>
    public string? RelationshipToPolicyholder { get; init; }
}

/// <summary>
/// Details of the incident or loss giving rise to the claim.
/// </summary>
public sealed record IncidentInfo
{
    /// <summary>Date on which the incident or loss occurred.</summary>
    [Required]
    public required DateOnly IncidentDate { get; init; }

    /// <summary>Free-text account of what happened.</summary>
    public string? Description { get; init; }

    /// <summary>Total amount being claimed, in <see cref="Currency"/>.</summary>
    [Range(0.01, double.MaxValue)]
    public decimal ClaimAmount { get; init; }

    /// <summary>ISO 4217 currency code for <see cref="ClaimAmount"/>.</summary>
    [Required]
    public required string Currency { get; init; }
}

/// <summary>
/// Information about the policy against which the claim is made.
/// </summary>
public sealed record PolicyInfo
{
    /// <summary>Number identifying the policy being claimed against.</summary>
    [Required]
    public required string PolicyNumber { get; init; }

    /// <summary>National identifier of the policyholder, used to match the claim to the policy.</summary>
    [Required]
    public required string PolicyholderIdNumber { get; init; }
}

/// <summary>
/// Account into which an approved claim will be settled.
/// </summary>
public sealed record BankingDetails
{
    /// <summary>Name the account is registered in.</summary>
    [Required]
    public required string AccountHolder { get; init; }

    /// <summary>Number of the account to be paid.</summary>
    [Required]
    public required string AccountNumber { get; init; }

    /// <summary>Branch or routing code for the account.</summary>
    [Required]
    public required string BranchCode { get; init; }

    /// <summary>Name of the bank holding the account.</summary>
    [Required]
    public required string BankName { get; init; }
}
