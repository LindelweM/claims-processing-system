using System.Text.Json;
using Claims.Contracts.Enums;
using Claims.Contracts.Intake;
using Claims.Domain;
using Claims.Domain.Sla;
using Claims.Functions.Models;
using Claims.Functions.Orchestrations;
using Claims.Functions.Validation;
using Claims.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace Claims.Functions.Http;

/// <summary>
/// The intake endpoint. Accepts the submission produced by the web form, persists the claim in
/// <see cref="ClaimStatus.Received"/>, and starts the durable orchestration that works it.
/// </summary>
/// <remarks>
/// The orchestration's instance id is the claim id. That gives the rest of the system one
/// identifier for both the claim and its run: a status lookup and a settlement callback can
/// each address the orchestration knowing only which claim they are about, with no lookup table
/// and no second identifier to keep in step.
/// </remarks>
public sealed class SubmitClaimFunction
{
    /// <summary>
    /// Enums are written as names and read as either names or numbers, so a channel can send
    /// back exactly what it was given.
    /// </summary>
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly ILogger<SubmitClaimFunction> _logger;
    private readonly IClaimRepository _claims;
    private readonly SlaPolicy _slaPolicy;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the endpoint over the claims store and the SLA policy.</summary>
    /// <param name="logger">Log for recording accepted submissions.</param>
    /// <param name="claims">Store the claim is written to.</param>
    /// <param name="slaPolicy">Turnaround targets used to set the claim's deadline.</param>
    /// <param name="timeProvider">Clock the submission is timed by.</param>
    public SubmitClaimFunction(
        ILogger<SubmitClaimFunction> logger,
        IClaimRepository claims,
        SlaPolicy slaPolicy,
        TimeProvider timeProvider)
    {
        _logger = logger;
        _claims = claims;
        _slaPolicy = slaPolicy;
        _timeProvider = timeProvider;
    }

    /// <summary>Handles a claim submission.</summary>
    /// <param name="request">Incoming HTTP request carrying the submission.</param>
    /// <param name="durableClient">Client used to start the processing orchestration.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>
    /// 202 with the acknowledgement for a new claim; 200 with the original acknowledgement when
    /// the channel resends a reference it has already lodged; 400 when the submission is invalid.
    /// </returns>
    [Function("SubmitClaim")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "claims")] HttpRequest request,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        ClaimSubmissionRequest? submission;
        try
        {
            submission = await JsonSerializer.DeserializeAsync<ClaimSubmissionRequest>(
                request.Body, JsonSerializerOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Claim submission could not be deserialized");
            return Json(StatusCodes.Status400BadRequest, new { error = "Request body is not a valid claim submission.", detail = ex.Message });
        }

        if (submission is null)
        {
            return Json(StatusCodes.Status400BadRequest, new { error = "Request body was empty." });
        }

        var submittedAt = _timeProvider.GetUtcNow();

        var errors = ClaimSubmissionValidator.Validate(submission, DateOnly.FromDateTime(submittedAt.UtcDateTime));
        if (errors.Count > 0)
        {
            _logger.LogInformation("Claim submission rejected with {ErrorCount} validation errors", errors.Count);
            return Json(StatusCodes.Status400BadRequest, new { error = "The submission is invalid.", errors });
        }

        if (submission.ChannelReference is { } channelReference)
        {
            var existing = await _claims.GetByChannelReferenceAsync(submission.Channel, channelReference, cancellationToken);
            if (existing is not null)
            {
                return await AcknowledgeResubmissionAsync(existing, request, durableClient, cancellationToken);
            }
        }

        var claim = CreateClaim(submission, submittedAt);

        if (!await _claims.TryAddAsync(claim, cancellationToken))
        {
            // A concurrent resubmission saved first.
            var existing = await _claims.GetByChannelReferenceAsync(submission.Channel, submission.ChannelReference!, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Claim for {submission.Channel} reference {submission.ChannelReference} was refused as a duplicate but could not be found.");

            return await AcknowledgeResubmissionAsync(existing, request, durableClient, cancellationToken);
        }

        await StartOrchestrationAsync(claim, request, durableClient, cancellationToken);

        return Json(StatusCodes.Status202Accepted, Acknowledge(claim, "Claim received and queued for assessment."));
    }

    private Claim CreateClaim(ClaimSubmissionRequest submission, DateTimeOffset submittedAt)
    {
        var priority = PriorityResolver.Resolve(submission.ClaimType);
        var deadline = _slaPolicy.DeadlineFrom(priority, submittedAt);
        var reference = Claim.BuildReference(submission.ClaimType, submittedAt);

        var claim = Claim.Submit(
            reference,
            submission.Channel,
            submission.ChannelReference,
            submission.ClaimType,
            submission.Policy.PolicyNumber,
            submission.Policy.PolicyholderIdNumber,
            submission.Incident.IncidentDate,
            submission.Incident.ClaimAmount,
            submission.Incident.Currency,
            submittedAt,
            deadline,
            priority);

        // Claimant and banking details are held on the claim so the activities can call the
        // registry and the payment provider without the submission travelling with them.
        claim.RecordClaimant(
            submission.Claimant.FirstName,
            submission.Claimant.LastName,
            submission.Claimant.IdentityNumber ?? submission.Policy.PolicyholderIdNumber);

        claim.RecordBankingDetails(
            submission.BankingDetails.AccountHolder,
            submission.BankingDetails.AccountNumber,
            submission.BankingDetails.BranchCode,
            submission.BankingDetails.BankName);

        return claim;
    }

    /// <summary>
    /// Answers a resubmission with the claim it already created. Also starts the orchestration
    /// if it never started, which is what a channel retrying after a failed request needs.
    /// </summary>
    private async Task<IActionResult> AcknowledgeResubmissionAsync(
        Claim claim,
        HttpRequest request,
        DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Resubmission of {Channel} reference {ChannelReference} resolved to claim {ClaimReference}",
            claim.Channel,
            claim.ChannelReference,
            claim.ClaimReference);

        var instance = await durableClient.GetInstanceAsync(claim.Id.ToString(), cancellationToken);
        if (instance is null)
        {
            _logger.LogWarning("Claim {ClaimReference} had no orchestration; starting it now", claim.ClaimReference);
            await StartOrchestrationAsync(claim, request, durableClient, cancellationToken);
        }

        return Json(StatusCodes.Status200OK, Acknowledge(claim, "Claim already received; this is the original acknowledgement."));
    }

    private async Task StartOrchestrationAsync(
        Claim claim,
        HttpRequest request,
        DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        // The instance id is the claim id so the payment callback can find this orchestration
        // knowing only which claim settled.
        var instanceId = await durableClient.ScheduleNewOrchestrationInstanceAsync(
            nameof(ClaimProcessingOrchestrator),
            BuildOrchestrationInput(claim, request),
            new StartOrchestrationOptions(InstanceId: claim.Id.ToString()),
            cancellationToken);

        _logger.LogInformation(
            "Claim {ClaimReference} accepted, orchestration {InstanceId} started",
            claim.ClaimReference,
            instanceId);
    }

    private static ClaimSubmissionResponse Acknowledge(Claim claim, string message) =>
        new()
        {
            ClaimId = claim.Id,
            ClaimReference = claim.ClaimReference,
            Status = claim.Status,
            Priority = claim.Priority,
            Deadline = claim.Deadline,
            Message = message
        };

    private static ContentResult Json(int statusCode, object payload) =>
        new()
        {
            Content = JsonSerializer.Serialize(payload, JsonSerializerOptions),
            ContentType = "application/json; charset=utf-8",
            StatusCode = statusCode
        };

    /// <summary>Builds the input the orchestration is started with.</summary>
    private static ClaimProcessingInput BuildOrchestrationInput(Claim claim, HttpRequest request) =>
        new()
        {
            ClaimId = claim.Id,
            Priority = claim.Priority,
            SlaDeadline = claim.Deadline,
            PaymentCallbackUrl =
                $"{request.Scheme}://{request.Host}/api/claims/{claim.Id}/payment-callback"
        };
}
