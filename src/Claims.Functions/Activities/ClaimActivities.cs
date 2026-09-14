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

public class ClaimActivities
{
    private readonly IClaimRepository _claimsRepository;
    private readonly IClientRegistryClient _clientRegistryClient;
    private readonly IPolicyManagerClient _policyManagerClient;
    private readonly IPaymentClient _paymentClient;
    private readonly ILogger<ClaimActivities> _logger;

    public ClaimActivities(
        IClaimRepository claimsRepository,
        IClientRegistryClient clientRegistryClient,
        IPolicyManagerClient policyManagerClient,
        IPaymentClient paymentClient,
        ILogger<ClaimActivities> logger)
    {
        this._claimsRepository = claimsRepository;
        this._clientRegistryClient = clientRegistryClient;
        this._policyManagerClient = policyManagerClient;
        this._paymentClient = paymentClient;
        this._logger = logger;
    }

    [Function(nameof(MarkValidatingActivity))]
    public async Task MarkValidatingActivity(
        [ActivityTrigger] Guid claimId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking claim {ClaimId} as validating", claimId);

        var claim = await LoadAsync(claimId, cancellationToken);
        claim.MarkValidating(DateTimeOffset.UtcNow);
        await _claimsRepository.SaveChangesAsync(cancellationToken);
    }


    [Function(nameof(VerifyClientActivity))]
    public async Task<ClientValidationActivityResult> VerifyClientActivity(
        [ActivityTrigger] Guid claimId,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(claimId, cancellationToken);

        _logger.LogInformation(
            "Validating client for claim {ClaimId}",
            claim.Id);

        var result = await _clientRegistryClient.ValidateAsync(
            new ClientValidationRequest
            {
                ClaimId = claim.Id,
                IdNumber = claim.ClaimantIdNumber,
                FirstName = claim.ClaimantFirstName,
                LastName = claim.ClaimantLastName,
                PolicyholderIdNumber = claim.PolicyholderIdNumber,
            }
            , cancellationToken);

        if (result.IsValid)
        {
            _logger.LogInformation("Client validation passed for claim {ClaimId}", claim.Id);

            claim.MarkClientValidated(result.ClientId!, DateTimeOffset.UtcNow);
            await _claimsRepository.SaveChangesAsync(cancellationToken);
        }
        return new ClientValidationActivityResult
        {
            IsValid = result.IsValid,
            ClientId = result.ClientId,
            FailureReason = result.FailureReason
        };
    }

    [Function(nameof(VerifyPolicyActivity))]
    public async Task<bool> VerifyPolicyActivity([ActivityTrigger] ValidatePolicyActivityInput input, CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);

        _logger.LogInformation(
            "Validating policy for claim {ClaimId}",
            claim.Id);

        var result = await _policyManagerClient.VerifyAsync(
            new PolicyVerificationRequest
            {
                ClaimId = claim.Id,
                PolicyNumber = claim.PolicyNumber,
                ClientId = input.ClientId,
                ClaimType = claim.Type,
                ClaimAmount = claim.ClaimAmount,
                Currency = claim.Currency,
                IncidentDate = claim.IncidentDate
            },
            cancellationToken);

        if (result.IsValid)
        {
            claim.MarkPolicyValidated(result.ApprovedAmount, result.Currency!, DateTimeOffset.UtcNow);
            await _claimsRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Policy validation passed for claim {ClaimId}", claim.Id);
            return true;
        }
       claim.Reject(result.FailureReason!, DateTimeOffset.UtcNow);
        await _claimsRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Policy validation failed for claim {ClaimId}: {FailureReason}", claim.Id, result.FailureReason);
        return false;
    }

    [Function(nameof(ApproveClaimActivity))]
    public async Task ApproveClaimActivity([ActivityTrigger] Guid claimId, CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(claimId, cancellationToken);

        var approvedAmount = claim.ApprovedAmount
            ?? throw new InvalidOperationException(
                $"Claim {claim.ClaimReference} has no approved amount to approve against.");

        claim.Approve(approvedAmount, DateTimeOffset.UtcNow);
        await _claimsRepository.SaveChangesAsync(cancellationToken);
    }

    [Function(nameof(RequestPaymentActivity))]
    public async Task<PaymentInitiationActivityResult> RequestPaymentActivity(
        [ActivityTrigger] InitiatePaymentActivityInput request,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(request.ClaimId, cancellationToken);

        // Pay what the policy manager authorised, which can be less than what was claimed.
        var approvedAmount = claim.ApprovedAmount
            ?? throw new InvalidOperationException(
                $"Claim {claim.ClaimReference} has no approved amount to pay.");

        _logger.LogInformation(
            "Requesting payment for claim {ClaimId}",
            claim.Id);

        var result = await _paymentClient.RequestPaymentAsync(
            new PaymentRequest
            {
                ClaimId = claim.Id,
                ClaimReference = claim.ClaimReference,
                Amount = approvedAmount,
                Currency = claim.Currency,
                AccountHolder = claim.AccountHolder,
                AccountNumber = claim.AccountNumber,
                BranchCode = claim.BranchCode,
                CallbackUrl = request.CallbackUrl
            },
            cancellationToken);

        if (result.IsAccepted)
        {
            claim.MarkPaymentRequested(result.PaymentReference!, DateTimeOffset.UtcNow);
            await _claimsRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Payment request accepted for claim {ClaimId} with reference {PaymentReference}", claim.Id, result.PaymentReference);
        }
        else
        {
            claim.MarkPaymentFailed(result.FailureReason!, DateTimeOffset.UtcNow);
            await _claimsRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Payment request rejected for claim {ClaimId}: {FailureReason}", claim.Id, result.FailureReason);
        }

        return new PaymentInitiationActivityResult
        {
            Accepted = result.IsAccepted,
            PaymentReference = result.PaymentReference,
            FailureReason = result.FailureReason
        };
    }

    [Function(nameof(HandlePaymentCompletionActivity))]
    public async Task<bool> HandlePaymentCompletionActivity(
        [ActivityTrigger] PaymentCompletionActivityInput input,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);
        var notification = input.Notification;

        _logger.LogInformation(
            "Handling payment completion for claim {ClaimId} with status {Status}",
            claim.Id,
            input.Notification.Status);

        if (input.Notification.Status == PaymentStatus.Succeeded)
        {
            claim.MarkPaid(notification.SettledAt ?? DateTimeOffset.UtcNow);
            await _claimsRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Payment completed for claim {ClaimId} with reference {PaymentReference}", claim.Id, input.Notification.PaymentReference);
            return true;
        }
        
        
        claim.MarkPaymentFailed(
            input.Notification.FailureReason ?? "Payment did not settle.",
            notification.SettledAt ?? DateTimeOffset.UtcNow);
        await _claimsRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Payment failed for claim {ClaimId}: {FailureReason}", claim.Id, input.Notification.FailureReason);
       return false;
    }

    [Function(nameof(CompleteClaimActivity))]
    public async Task CompleteClaimActivity(
        [ActivityTrigger] Guid claimId,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(claimId, cancellationToken);

        _logger.LogInformation(
            "Completing claim {ClaimId}",
            claim.Id);

        claim.Completed(DateTimeOffset.UtcNow);
        await _claimsRepository.SaveChangesAsync(cancellationToken);
    }

     [Function(nameof(RejectClaimActivity))]
    public async Task RejectClaimActivity(
        [ActivityTrigger] RejectClaimsActivityInput input,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);

        _logger.LogInformation(
            "Rejecting claim {ClaimId}",
            claim.Id);

        claim.Reject(input.Reason, DateTimeOffset.UtcNow);
        await _claimsRepository.SaveChangesAsync(cancellationToken);
    }

    [Function(nameof(FailClaimActivity))]
    public async Task FailClaimActivity(
        [ActivityTrigger] RejectClaimsActivityInput input,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken); 
        claim.Fail(input.Reason, DateTimeOffset.UtcNow);
        await _claimsRepository.SaveChangesAsync(cancellationToken);     
    }

    [Function(nameof(FlagSlaBreachActivity))]
    public async Task FlagSlaBreachActivity(
        [ActivityTrigger] SlaBreachActivityInput input,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);
        claim.FlagSlaBreach(DateTimeOffset.UtcNow);
        await _claimsRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<Domain.Claim> LoadAsync(Guid claimId, CancellationToken cancellationToken)
    {
        var claim = await _claimsRepository.GetByIdAsync(claimId, cancellationToken);
        if (claim is null)
        {
            throw new InvalidOperationException($"Claim {claimId} not found");
        }
        return claim;
    }

}