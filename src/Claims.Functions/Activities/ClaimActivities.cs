using Claims.Contracts.Enums;
using Claims.Contracts.Integration;
using Claims.Functions.Models;
using Claims.Integration.ClientRegistry;
using Claims.Integration.Payments;
using Claims.Integration.PolicyManager;
using Claims.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Claims.Functions.Activities;

/// <summary>
/// The steps the claim orchestration runs, each one loading the claim it works on so no
/// claim detail has to travel through the orchestration's history.
/// </summary>
public sealed class ClaimActivities(
    IClientRegistryClient registry,
    IPolicyManagerClient policies,
    IPaymentClient payments,
    IClaimRepository claims,
    ILogger<ClaimActivities> logger)
{
    /// <summary>Name the orchestrator calls <see cref="ValidateClientAsync"/> by.</summary>
    public const string ValidateClient = nameof(ValidateClient);

    /// <summary>Name the orchestrator calls <see cref="ValidatePolicyAsync"/> by.</summary>
    public const string ValidatePolicy = nameof(ValidatePolicy);

    /// <summary>Name the orchestrator calls <see cref="InitiatePaymentAsync"/> by.</summary>
    public const string InitiatePayment = nameof(InitiatePayment);

    /// <summary>Name the orchestrator calls <see cref="CompletePaymentAsync"/> by.</summary>
    public const string CompletePayment = nameof(CompletePayment);

    /// <summary>Name the orchestrator calls <see cref="RejectClaimAsync"/> by.</summary>
    public const string RejectClaim = nameof(RejectClaim);

    /// <summary>Name the orchestrator calls <see cref="FlagSlaBreachAsync"/> by.</summary>
    public const string FlagSlaBreach = nameof(FlagSlaBreach);

    /// <summary>
    /// Resolves the claimant against the client registry and links the matched client to the claim.
    /// </summary>
    /// <param name="claimId">Claim whose claimant is being validated.</param>
    /// <param name="cancellationToken">Token to cancel the activity.</param>
    /// <returns>The registry's verdict, for the orchestrator to branch on.</returns>
    [Function(ValidateClient)]
    public async Task<ClientValidationActivityResult> ValidateClientAsync(
        [ActivityTrigger] Guid claimId,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(claimId, cancellationToken);

        claim.MarkValidating(DateTimeOffset.UtcNow);

        var result = await registry.ValidateAsync(
            new ClientValidationRequest
            {
                ClaimId = claim.Id,
                IdNumber = claim.ClaimantIdNumber,
                FirstName = claim.ClaimantFirstName,
                LastName = claim.ClaimantLastName,
                PolicyholderIdNumber = claim.PolicyholderIdNumber
            },
            cancellationToken);

        if (result.IsValid && result.ClientId is not null)
        {
            claim.MarkClientValidated(result.ClientId, DateTimeOffset.UtcNow);
        }
        else
        {
            logger.LogWarning(
                "Claim {ClaimReference} failed client validation: {Reason}",
                claim.ClaimReference,
                result.FailureReason);
        }

        // Saved either way: the claim entering validation is itself worth recording.
        await claims.SaveChangesAsync(cancellationToken);

        return new ClientValidationActivityResult
        {
            IsValid = result.IsValid,
            ClientId = result.ClientId,
            FailureReason = result.FailureReason
        };
    }

    /// <summary>
    /// Confirms with the policy manager that the policy covers the claim, and approves the
    /// claim for the amount authorised.
    /// </summary>
    /// <param name="input">Claim to verify, and the client it was matched to.</param>
    /// <param name="cancellationToken">Token to cancel the activity.</param>
    /// <returns>The policy manager's verdict, for the orchestrator to branch on.</returns>
    [Function(ValidatePolicy)]
    public async Task<PolicyVerificationResponse> ValidatePolicyAsync(
        [ActivityTrigger] ValidatePolicyActivityInput input,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);

        var result = await policies.VerifyAsync(
            new PolicyVerificationRequest
            {
                ClaimId = claim.Id,
                PolicyNumber = claim.PolicyNumber,
                ClientId = input.ClientId,
                IncidentDate = claim.IncidentDate,
                ClaimType = claim.Type,
                ClaimAmount = claim.ClaimAmount,
                Currency = claim.Currency
            },
            cancellationToken);

        if (result.IsValid)
        {
            claim.MarkPolicyValidated(
                result.ApprovedAmount, result.Currency ?? claim.Currency, DateTimeOffset.UtcNow);

            claim.Approve(result.ApprovedAmount, DateTimeOffset.UtcNow);

            await claims.SaveChangesAsync(cancellationToken);
        }
        else
        {
            logger.LogWarning(
                "Claim {ClaimReference} declined by policy manager: {Reason}",
                claim.ClaimReference,
                result.FailureReason);
        }

        return result;
    }

    /// <summary>
    /// Instructs the payment provider to disburse the approved claim.
    /// </summary>
    /// <param name="input">Claim to pay, and where the provider reports settlement.</param>
    /// <param name="cancellationToken">Token to cancel the activity.</param>
    /// <returns>The provider's acknowledgement, for the orchestrator to branch on.</returns>
    [Function(InitiatePayment)]
    public async Task<PaymentInitiationActivityResults> InitiatePaymentAsync(
        [ActivityTrigger] InitiatePaymentActivityInput input,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);

        // Paying the approved amount, never the amount claimed.
        var amount = claim.ApprovedAmount
            ?? throw new InvalidOperationException(
                $"Claim {claim.ClaimReference} has no approved amount to pay.");

        var result = await payments.RequestPaymentAsync(
            new PaymentRequest
            {
                ClaimId = claim.Id,
                ClaimReference = claim.ClaimReference,
                Amount = amount,
                Currency = claim.Currency,
                AccountHolder = claim.AccountHolder,
                AccountNumber = claim.AccountNumber,
                BranchCode = claim.BranchCode,
                CallbackUrl = input.CallbackUrl
            },
            cancellationToken);

        if (result.IsAccepted && result.PaymentReference is not null)
        {
            claim.MarkPaymentRequested(result.PaymentReference, DateTimeOffset.UtcNow);
        }
        else
        {
            logger.LogWarning(
                "Payment for claim {ClaimReference} was refused: {Reason}",
                claim.ClaimReference,
                result.FailureReason);

            claim.MarkPaymentFailed(
                result.FailureReason ?? "Payment provider refused the instruction.",
                DateTimeOffset.UtcNow);
        }

        await claims.SaveChangesAsync(cancellationToken);

        return new PaymentInitiationActivityResults
        {
            Accepted = result.IsAccepted,
            PaymentReference = result.PaymentReference,
            FailureReason = result.FailureReason
        };
    }

    /// <summary>
    /// Applies the settlement outcome the provider reported, completing or failing the claim.
    /// </summary>
    /// <param name="input">Claim and the notification the provider sent.</param>
    /// <param name="cancellationToken">Token to cancel the activity.</param>
    /// <returns>True when the payment settled.</returns>
    [Function(CompletePayment)]
    public async Task<bool> CompletePaymentAsync(
        [ActivityTrigger] PaymentCompletionActivityInput input,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);

        var settled = input.Notification.Status == PaymentStatus.Succeeded;

        if (settled)
        {
            claim.MarkPaid(input.Notification.SettledAt ?? DateTimeOffset.UtcNow);
            claim.Completed(DateTimeOffset.UtcNow);
        }
        else
        {
            logger.LogWarning(
                "Payment {PaymentReference} for claim {ClaimReference} did not settle: {Reason}",
                input.Notification.PaymentReference,
                claim.ClaimReference,
                input.Notification.FailureReason);

            claim.MarkPaymentFailed(
                input.Notification.FailureReason ?? "Payment did not settle.",
                DateTimeOffset.UtcNow);
        }

        await claims.SaveChangesAsync(cancellationToken);

        return settled;
    }

    /// <summary>Rejects a claim that was refused on its merits.</summary>
    /// <param name="input">Claim to reject, and why.</param>
    /// <param name="cancellationToken">Token to cancel the activity.</param>
    [Function(RejectClaim)]
    public async Task RejectClaimAsync(
        [ActivityTrigger] RejectClaimsActivityInput input,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);

        logger.LogInformation(
            "Rejecting claim {ClaimReference}: {Reason}", claim.ClaimReference, input.Reason);

        claim.Reject(input.Reason, DateTimeOffset.UtcNow);

        await claims.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Flags a claim as having missed its assessment deadline, leaving its state alone so it
    /// stays in whichever queue it is already in.
    /// </summary>
    /// <param name="input">Claim that is overdue, and a note for its audit trail.</param>
    /// <param name="cancellationToken">Token to cancel the activity.</param>
    [Function(FlagSlaBreach)]
    public async Task FlagSlaBreachAsync(
        [ActivityTrigger] SlaBreachActivityInput input,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);

        claim.FlagSlaBreach(DateTimeOffset.UtcNow);
        claim.AddHistory(claim.Status, DateTimeOffset.UtcNow, input.Detail);

        await claims.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Loads the claim an activity is working on, or fails loudly if it has gone.</summary>
    private async Task<Domain.Claim> LoadAsync(Guid claimId, CancellationToken cancellationToken) =>
        await claims.GetByIdAsync(claimId, cancellationToken)
        ?? throw new InvalidOperationException($"Claim {claimId} was not found.");
}
