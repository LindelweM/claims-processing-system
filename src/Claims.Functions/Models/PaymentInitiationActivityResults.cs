namespace Claims.Functions.Models;

/// <summary>
/// What the payment activity reports back to the orchestration: whether the provider took the
/// instruction, and either the reference to settle against or the reason it was refused.
/// </summary>
/// <remarks>
/// Acceptance is not settlement. A true <see cref="Accepted"/> only means the provider has the
/// instruction; the orchestration still waits for the settlement callback before completing
/// the claim.
/// </remarks>
public sealed class PaymentInitiationActivityResults
{
    /// <summary>True when the provider accepted the instruction for processing.</summary>
    public required bool Accepted { get; init; }

    /// <summary>
    /// Provider's identifier for the payment, set when <see cref="Accepted"/> is true. Recorded
    /// on the claim and quoted back on the settlement callback.
    /// </summary>
    public string? PaymentReference { get; init; }

    /// <summary>
    /// Why the provider refused the instruction, set when <see cref="Accepted"/> is false.
    /// </summary>
    public string? FailureReason { get; init; }
}
