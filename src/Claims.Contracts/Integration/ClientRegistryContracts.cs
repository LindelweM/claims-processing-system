namespace Claims.Contracts.Integration;

/// <summary>
/// Request to the client registry to resolve and verify the party lodging a claim.
/// </summary>
public sealed record ClientValidationRequest
{
    /// <summary>Claim the validation is being performed for.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>National identifier of the claimant to resolve.</summary>
    public required string IdNumber { get; init; }

    /// <summary>Given name as captured at intake, used to corroborate the match.</summary>
    public required string FirstName { get; init; }

    /// <summary>Family name as captured at intake, used to corroborate the match.</summary>
    public required string LastName { get; init; }

    /// <summary>National identifier of the policyholder, used to confirm the claimant's link to the policy.</summary>
    public required string PolicyholderIdNumber { get; init; }
}

/// <summary>
/// Client registry's verdict on whether the claimant could be resolved and verified.
/// </summary>
public sealed record ClientValidationResult
{
    /// <summary>True when the registry matched and verified the claimant.</summary>
    public required bool IsValid { get; init; }

    /// <summary>Registry's identifier for the matched client, set when <see cref="IsValid"/> is true.</summary>
    public string? ClientId { get; init; }

    /// <summary>Why validation failed, set when <see cref="IsValid"/> is false.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Creates a successful result for the client the registry matched.</summary>
    /// <param name="clientId">Registry's identifier for the matched client.</param>
    public static ClientValidationResult Valid(string clientId) => new()
    {
        IsValid = true,
        ClientId = clientId
    };

    /// <summary>Creates a failed result explaining why the claimant could not be validated.</summary>
    /// <param name="reason">Why validation failed.</param>
    public static ClientValidationResult Invalid(string reason) => new()
    {
        IsValid = false,
        FailureReason = reason
    };
}
