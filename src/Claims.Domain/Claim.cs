using Claims.Contracts.Enums;
using PaymentState = Claims.Contracts.Enums.PaymentStatus;

namespace Claims.Domain;

/// <summary>
/// A claim lodged against a policy, and the aggregate that owns its lifecycle.
/// </summary>
public sealed class Claim
{
    private readonly List<ClaimStatusHistory> _history = [];

    /// <summary>System-assigned identifier for the claim.</summary>
    public Guid Id { get; private set; } = Guid.NewGuid();

    /// <summary>Human-readable reference quoted to the claimant.</summary>
    public string ClaimReference { get; private set; } = null!;

    /// <summary>Category of cover being claimed.</summary>
    public ClaimType Type { get; private set; }

    /// <summary>State the claim is currently in.</summary>
    public ClaimStatus Status { get; private set; }

    /// <summary>Processing urgency the claim is worked at.</summary>
    public ClaimPriority Priority { get; private set; }

    /// <summary>Number of the policy the claim is made against.</summary>
    public string PolicyNumber { get; private set; } = null!;

    /// <summary>National identifier of the policyholder.</summary>
    public string PolicyholderIdNumber { get; private set; } = null!;

    /// <summary>Client registry's identifier for the claimant, set once the claimant is validated.</summary>
    public string? ClientId { get; private set; }

    /// <summary>Date on which the incident or loss occurred.</summary>
    public DateOnly IncidentDate { get; private set; }

    /// <summary>Amount being claimed, in <see cref="Currency"/>.</summary>
    public decimal ClaimAmount { get; private set; }

    /// <summary>ISO 4217 currency code for the monetary amounts on this claim.</summary>
    public string Currency { get; private set; } = null!;

    /// <summary>Amount the policy manager approved, set once the claim is assessed.</summary>
    public decimal? ApprovedAmount { get; private set; }

    /// <summary>State of the disbursement raised against this claim. Null until a payment is due.</summary>
    public PaymentState? PaymentStatus { get; private set; }

    /// <summary>Payment provider's reference, set once a payment is instructed.</summary>
    public string? PaymentReference { get; private set; }

    /// <summary>When the claim was submitted.</summary>
    public DateTimeOffset SubmittedDate { get; private set; }

    /// <summary>Date by which the claim is expected to be assessed.</summary>
    public DateTimeOffset Deadline { get; private set; }

    /// <summary>When the claim was last changed.</summary>
    public DateTimeOffset LastUpdatedAt { get; private set; }

    /// <summary>
    /// Why the claim was rejected, could not be processed, or failed to pay out. Set by
    /// <see cref="Reject"/>, <see cref="Fail"/> and <see cref="MarkPaymentFailed"/>.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>Audit trail of every state the claim has moved through, oldest first.</summary>
    public IReadOnlyList<ClaimStatusHistory> History => _history;

    /// <summary>
    /// True once a breach has been recorded by <see cref="FlagSlaBreach"/>. This is a stored
    /// flag, not a live calculation, so it stays false until something flags it. Use
    /// <see cref="IsSlaBreached"/> to evaluate the deadline as at a point in time.
    /// </summary>
    public bool SlaBreached { get; private set; }

    private Claim()
    {
    }

    /// <summary>
    /// Lodges a new claim, placing it in <see cref="ClaimStatus.Submitted"/> and opening its audit trail.
    /// </summary>
    /// <param name="claimReference">Human-readable reference quoted to the claimant.</param>
    /// <param name="type">Category of cover being claimed.</param>
    /// <param name="policyNumber">Number of the policy being claimed against.</param>
    /// <param name="policyholderIdNumber">National identifier of the policyholder.</param>
    /// <param name="incidentDate">Date on which the incident occurred.</param>
    /// <param name="claimAmount">Amount being claimed.</param>
    /// <param name="currency">ISO 4217 currency code for <paramref name="claimAmount"/>.</param>
    /// <param name="submittedDate">When the claim was submitted.</param>
    /// <param name="deadline">Date by which the claim is expected to be assessed.</param>
    /// <param name="priority">Priority to work the claim at.</param>
    /// <returns>The newly lodged claim.</returns>
    public static Claim Submit(
        string claimReference,
        ClaimType type,
        string policyNumber,
        string policyholderIdNumber,
        DateOnly incidentDate,
        decimal claimAmount,
        string currency,
        DateTimeOffset submittedDate,
        DateTimeOffset deadline,
        ClaimPriority priority)
    {
        var claim = new Claim
        {
            ClaimReference = claimReference,
            Type = type,
            Status = ClaimStatus.Received,
            Priority = priority,
            PolicyNumber = policyNumber,
            PolicyholderIdNumber = policyholderIdNumber,
            IncidentDate = incidentDate,
            ClaimAmount = claimAmount,
            Currency = currency,
            SubmittedDate = submittedDate,
            Deadline = deadline,
            LastUpdatedAt = submittedDate
        };

        claim._history.Add(ClaimStatusHistory.Record(
            claim.Id, ClaimStatus.Received, submittedDate, detail: "Claim received"));

        return claim;
    }

    /// <summary>Moves the claim into validation, where the claimant and policy are checked.</summary>
    /// <param name="occurredAt">When validation started.</param>
    public void MarkValidating(DateTimeOffset occurredAt) =>
        Transition(ClaimStatus.Validating, occurredAt, "Validation started");

    /// <summary>
    /// Records that the client registry matched the claimant, linking the claim to the client.
    /// Leaves the claim in its current state.
    /// </summary>
    /// <param name="clientId">Registry's identifier for the matched client.</param>
    /// <param name="occurredAt">When the claimant was validated.</param>
    public void MarkClientValidated(string clientId, DateTimeOffset occurredAt)
    {
        ClientId = clientId;
        Transition(ClaimStatus.ClientValidated, occurredAt, $"Client validated against registry ({clientId})");
    }

    /// <summary>
    /// Records that the policy manager confirmed cover, moving the claim on to assessment.
    /// </summary>
    /// <param name="occurredAt">When cover was confirmed.</param>
    /// <param name="detail">What the policy manager confirmed.</param>
    public void MarkPolicyValidated(decimal approvedAmount, string currency, DateTimeOffset occurredAt)
    {
        ApprovedAmount = approvedAmount;
        Currency = currency;
        Transition(ClaimStatus.ClientValidated, occurredAt, $"Policy validated, approved {approvedAmount:0.00} {currency}");
    }

    /// <summary>
    /// Approves the claim for the amount the policy manager authorised, moving it to
    /// <see cref="ClaimStatus.Approved"/>, or to <see cref="ClaimStatus.PartiallyApproved"/>
    /// when less than the amount claimed was authorised.
    /// </summary>
    /// <param name="approvedAmount">Amount approved, which may be less than the amount claimed.</param>
    /// <param name="occurredAt">When the approval was made.</param>
    public void Approve(decimal approvedAmount, DateTimeOffset occurredAt)
    {
        Transition(ClaimStatus.Approved, occurredAt, $"Approved for {approvedAmount:0.00} {Currency}");
    
    }

    /// <summary>Rejects the claim, closing it.</summary>
    /// <param name="reason">Why the claim was rejected.</param>
    /// <param name="occurredAt">When the rejection was made.</param>
    public void Reject(string reason, DateTimeOffset occurredAt)
    {
        Transition(ClaimStatus.Rejected, occurredAt, reason);
        FailureReason = reason;
    }

    /// <summary>
    /// Records that a payment has been instructed against the approved claim.
    /// Leaves the claim in its current state until the provider reports the outcome.
    /// </summary>
    /// <param name="paymentReference">Payment provider's reference for the disbursement.</param>
    /// <param name="occurredAt">When the payment was instructed.</param>
    public void MarkPaymentRequested(string paymentReference, DateTimeOffset occurredAt)
    {
        PaymentReference = paymentReference;
        PaymentStatus = PaymentState.Pending;
        Transition(ClaimStatus.PaymentRequested, occurredAt, $"Payment requested ({paymentReference})");
    }

    /// <summary>Records that the payment cleared, settling the claim.</summary>
    /// <param name="occurredAt">When the payment settled.</param>
    public void MarkPaid(DateTimeOffset occurredAt)
    {
        PaymentStatus = PaymentState.Succeeded;
        Transition(ClaimStatus.Paid, occurredAt, "Payment settled");
    }

    /// <summary>
    /// Records that the payment did not go through. The claim stays approved so the
    /// payment can be reinstructed once the cause is resolved.
    /// </summary>
    /// <param name="reason">Provider's explanation for the failure.</param>
    /// <param name="occurredAt">When the failure was reported.</param>
    public void MarkPaymentFailed(string reason, DateTimeOffset occurredAt)
    {
        PaymentStatus = PaymentState.Failed;
        FailureReason = reason;
        Transition(ClaimStatus.PaymentFailed, occurredAt, $"Payment failed: {reason}");
    }

    /// <summary>Closes the claim once settlement is done and no further work is expected.</summary>
    /// <param name="occurredAt">When the claim was completed.</param>
    public void Completed(DateTimeOffset occurredAt) =>
        Transition(ClaimStatus.Completed, occurredAt, "Claim completed");

    /// <summary>
    /// Closes the claim because it could not be processed, recording why.
    /// </summary>
    /// <param name="reason">Why the claim could not be processed.</param>
    /// <param name="occurredAt">When processing failed.</param>
    public void Fail(string reason, DateTimeOffset occurredAt)
    {
        FailureReason = reason;
        Transition(ClaimStatus.Failed, occurredAt, $"Processing failed: {reason}");
    }

    /// <summary>
    /// Flags the claim as having missed its assessment deadline. Does not change the claim's
    /// state, so the claim stays in the queue it is already in.
    /// </summary>
    /// <param name="occurredAt">When the breach was detected.</param>
    public void FlagSlaBreach(DateTimeOffset occurredAt)
    {
        if (SlaBreached)
        {
            return;
        }

        SlaBreached = true;
        AddHistory(Status, occurredAt, $"SLA breached, deadline was {Deadline:u}");
        LastUpdatedAt = occurredAt;
    }

    /// <summary>
    /// Moves the claim into a new state and records the transition on its audit trail.
    /// </summary>
    /// <param name="toStatus">State to move the claim into.</param>
    /// <param name="occurredAt">When the transition occurred.</param>
    /// <param name="detail">Why the claim is moving.</param>
    /// <exception cref="InvalidOperationException">The claim has already reached a closed state.</exception>
    public void Transition(ClaimStatus toStatus, DateTimeOffset occurredAt, string? detail = null)
    {
        if (toStatus == Status)
        {
            return;
        }

        AddHistory(toStatus, occurredAt, detail);
        Status = toStatus;
        LastUpdatedAt = occurredAt;
    }

    /// <summary>
    /// Appends an entry to the claim's audit trail without changing its state. Use this to
    /// record a milestone that happens within a state, such as an integration call completing.
    /// </summary>
    /// <param name="status">State the claim is in as the entry is recorded.</param>
    /// <param name="occurredAt">When the recorded event happened.</param>
    /// <param name="detail">What happened.</param>
    public void AddHistory(ClaimStatus status, DateTimeOffset occurredAt, string? detail = null) =>
        _history.Add(ClaimStatusHistory.Record(Id, status, occurredAt, detail));

    /// <summary>
    /// Builds a claim reference of the form PREFIX-yyyyMMdd-XXXXXX, such as FNL-20260914-3A9C1F.
    /// </summary>
    /// <param name="type">Category of cover, which sets the prefix.</param>
    /// <param name="submittedAt">When the claim was submitted, which sets the date part.</param>
    /// <returns>A reference for quoting to the claimant.</returns>
    public static string BuildReference(ClaimType type, DateTimeOffset submittedAt)
    {
        var prefix = type switch
        {
            ClaimType.Death => "DTH",
            ClaimType.Funeral => "FNL",
            ClaimType.DreadDisease => "DRD",
            ClaimType.Disability => "DIS",
            _ => "CLM"
        };

        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        return $"{prefix}-{submittedAt:yyyyMMdd}-{suffix}";
    }
}
