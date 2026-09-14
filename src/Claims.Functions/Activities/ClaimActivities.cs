using Claims.Contracts.Integration;
using Claims.Functions.Models;
using Claims.Integration;
using Claims.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Claims.Functions.Activities;

public class ClaimActivities
{
    private readonly IClaimsRepository _claimsRepository;
    private readonly IClientRegistryClient _clientRegistryClient;
    private readonly IPolicyManagerClient _policyManagerClient;
    private readonly IPaymentClient _paymentClient;
    private readonly ILogger<ClaimActivities> _logger;

    public ClaimActivities(
        IClaimsRepository claimsRepository,
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
    public async Task<ClientValidationResult> VerifyClientActivity(
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
        return new ClientValidationResult
        {
            IsValid = result.IsValid,
            ClientId = result.ClientId,
            FailureReason = result.FailureReason
        };
    }

    [Function(nameof(VerifyPolicyActivity))]
    public async Task<PolicyValidationResult> VerifyPolicyActivity([ActivityTrigger] ValidatePolicyActivityInput input, CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(input.ClaimId, cancellationToken);

        _logger.LogInformation(
            "Validating policy for claim {ClaimId}",
            claim.Id);

        var result = await _policyManagerClient.ValidateAsync(
            new PolicyValidationRequest
            {
                ClaimId = claim.Id,
                PolicyNumber = claim.PolicyNumber,
                PolicyholderIdNumber = claim.PolicyholderIdNumber,
                ClientId = input.ClientId,
                ClaimType = claim.ClaimType,
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

    [Function(nameof(RequestPaymentActivity))]
    public async Task<PaymentInstructionResult> RequestPaymentActivity(
        [ActivityTrigger] InitiatePaymentActivityInput request,
        CancellationToken cancellationToken)
    {
        var claim = await LoadAsync(request.ClaimId, cancellationToken);
        var callbackUrl = $"{request.CallbackUrl?.TrimEnd('/')}/api/claims/{claim.Id}/payment-callback";

        _logger.LogInformation(
            "Requesting payment for claim {ClaimId}",
            claim.Id);

        var result = await _paymentClient.RequestPaymentAsync(
            new PaymentRequest
            {
                ClaimId = claim.Id,
                ClaimReference = claim.ClaimReference,
                Amount = claim.ClaimAmount,
                Currency = claim.Currency,
                AccountHolder = claim.AccountHolder,
                AccountNumber = claim.AccountNumber,
                BranchCode = claim.BranchCode,
                CallbackUrl = callbackUrl
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

        return new PaymentInitiationActivityResults
        {
            Accepted = result.IsAccepted,
            PaymentReference = result.PaymentReference,
            FailureReason = result.FailureReason
        };
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