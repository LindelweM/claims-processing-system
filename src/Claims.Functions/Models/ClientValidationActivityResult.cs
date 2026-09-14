namespace Claims.Functions.Models;

/// <summary>
/// What the client validation activity reports back to the orchestration: whether the claimant
/// was matched on the registry, and either the client they matched or the reason they did not.
/// </summary>
/// <remarks>
/// A claimant the registry refuses is a business outcome, not a fault, so it comes back as a
/// result the orchestration branches on rather than as an exception.
/// </remarks>
public sealed class ClientValidationActivityResult
{
    /// <summary>True when the registry matched and verified the claimant.</summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Registry's identifier for the matched client, set when <see cref="IsValid"/> is true.
    /// Carried into policy verification to confirm the claimant's link to the policy.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Why the claimant could not be validated, set when <see cref="IsValid"/> is false.
    /// Recorded against the claim when it is rejected.
    /// </summary>
    public string? FailureReason { get; init; }
}
