using Claims.Contracts.Enums;

namespace Claims.Contracts.Integration;

/// <summary>
/// Instruction to the payment provider to disburse an approved claim.
/// </summary>
public sealed record PaymentRequest
{
    /// <summary>Claim the payment settles.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>Human-readable claim reference, quoted on the payment.</summary>
    public required string ClaimReference { get; init; }

    /// <summary>Amount to disburse, in <see cref="Currency"/>.</summary>
    public required decimal Amount { get; init; }

    /// <summary>ISO 4217 currency code for <see cref="Amount"/>.</summary>
    public required string Currency { get; init; }

    /// <summary>Name the account being paid is registered in.</summary>
    public required string AccountHolder { get; init; }

    /// <summary>Number of the account to be paid.</summary>
    public required string AccountNumber { get; init; }

    /// <summary>Branch or routing code for the account.</summary>
    public required string BranchCode { get; init; }

    /// <summary>Endpoint the provider calls back with a <see cref="PaymentCompletionNotification"/>.</summary>
    public string? CallbackUrl { get; init; }
}

/// <summary>
/// Payment provider's acknowledgement of a disbursement instruction.
/// </summary>
public sealed record PaymentInstructionResult
{
    /// <summary>True when the provider accepted the instruction for processing.</summary>
    public required bool IsAccepted { get; init; }

    /// <summary>
    /// Provider's identifier for the payment, used for later status queries.
    /// Set when <see cref="IsAccepted"/> is true.
    /// </summary>
    public string? PaymentReference { get; init; }

    /// <summary>Provider's explanation when <see cref="IsAccepted"/> is false.</summary>
    public string? FailureReason { get; init; }

    /// <summary>Creates a result for an instruction the provider accepted.</summary>
    /// <param name="paymentReference">Provider's identifier for the payment.</param>
    public static PaymentInstructionResult Accepted(string paymentReference) => new()
    {
        IsAccepted = true,
        PaymentReference = paymentReference
    };

    /// <summary>Creates a result for an instruction the provider refused.</summary>
    /// <param name="failureReason">Why the provider refused the instruction.</param>
    public static PaymentInstructionResult Rejected(string failureReason) => new()
    {
        IsAccepted = false,
        FailureReason = failureReason
    };
}

/// <summary>
/// Callback from the provider reporting the final outcome of a payment.
/// </summary>
public sealed record PaymentCompletionNotification
{
    /// <summary>Provider's identifier for the payment.</summary>
    public required string PaymentReference { get; init; }

    /// <summary>Claim the payment settles.</summary>
    public required Guid ClaimId { get; init; }

    /// <summary>State the provider has settled the payment in.</summary>
    public required PaymentStatus Status { get; init; }

    /// <summary>When the payment cleared, where it has.</summary>
    public DateTimeOffset? SettledAt { get; init; }

    /// <summary>Provider's explanation when the payment failed or was reversed.</summary>
    public string? FailureReason { get; init; }
}
