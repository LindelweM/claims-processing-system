using Claims.Contracts.Integration;
using Claims.Functions.Activities;
using Claims.Functions.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;

namespace Claims.Functions.Orchestrations;

/// <summary>
/// Drives a claim from intake to settlement: validate the claimant, confirm cover, instruct
/// payment, then wait for the provider to report that the money moved.
/// </summary>
public static class ClaimProcessingOrchestrator
{

    /// <summary>Event the payment callback raises when the provider reports settlement.</summary>
    public const string PaymentCompletedEventName = "PaymentCompleted";

    /// <summary>
    /// Retry for activities that are safe to repeat: they read the claim, call a system that
    /// changes nothing, or apply a transition that is a no-op the second time. Absorbs SQL
    /// throttling and brief downstream outages without failing the claim.
    /// </summary>
    private static readonly TaskOptions Retryable = TaskOptions.FromRetryPolicy(new RetryPolicy(
        maxNumberOfAttempts: 5,
        firstRetryInterval: TimeSpan.FromSeconds(5),
        backoffCoefficient: 2.0,
        maxRetryInterval: TimeSpan.FromMinutes(2)));

    /// <summary>Runs the claim through validation, assessment and payment.</summary>
    /// <param name="context">Durable context this orchestration runs on.</param>
    /// <returns>A task that completes once the claim has been settled or ended.</returns>
    [Function(nameof(ClaimProcessingOrchestrator))]
    public static async Task RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<ClaimProcessingInput>()
            ?? throw new InvalidOperationException("Claim orchestration was started without input.");

        var logger = context.CreateReplaySafeLogger(nameof(ClaimProcessingOrchestrator));


        using var slaCts = new CancellationTokenSource();
        var slaTimer = CreateSlaTimer(context, input, slaCts.Token);
        var workTask = RunWorkflowAsync(context, input,logger);

        var winner = await Task.WhenAny(workTask, slaTimer);

        if (winner == slaTimer)
        {
            await context.CallActivityAsync(
                nameof(ClaimActivities.FlagSlaBreachActivity),
                new SlaBreachActivityInput
                {
                    ClaimId = input.ClaimId,
                    Detail = $"SLA deadline {input.SlaDeadline:u} elapsed before processing completed."
                },
                Retryable);

            logger.LogWarning("Claim {ClaimId} breached its SLA", input.ClaimId);
        }
        else
        {
            slaCts.Cancel();
        }

        // A breach flags the claim as late; it does not stop it being worked.
        await workTask;
    }

    private static async Task CreateSlaTimer(
        TaskOrchestrationContext context,
        ClaimProcessingInput input,
        CancellationToken cancellationToken)
    {
        if (input.SlaDeadline <= context.CurrentUtcDateTime)
        {
            return;
        }

        try
        {
            await context.CreateTimer(input.SlaDeadline.UtcDateTime, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            // The orchestration completed before the SLA deadline, so no breach.
        }

    }

    private static async Task RunWorkflowAsync(
        TaskOrchestrationContext context,
        ClaimProcessingInput input,
        ILogger logger)
    {
        try
        {
            await RunStepsAsync(context, input, logger);
        }
        catch (TaskFailedException ex)
        {
            // A step failed even after its retries. Record that on the claim so it does not sit
            // in an intermediate state that looks like it is still being worked.
            logger.LogError(
                "Claim {ClaimId} failed at {ActivityName}: {Message}",
                input.ClaimId,
                ex.TaskName,
                ex.FailureDetails.ErrorMessage);

            await context.CallActivityAsync(
                nameof(ClaimActivities.FailClaimActivity),
                new RejectClaimsActivityInput
                {
                    ClaimId = input.ClaimId,
                    Reason = ex.TaskName == nameof(ClaimActivities.RequestPaymentActivity)
                        ? "Payment instruction failed with an unknown outcome; reconcile with the provider before reinstructing."
                        : $"Processing stopped at {ex.TaskName} after repeated failures."
                },
                Retryable);
        }
    }

    private static async Task RunStepsAsync(
        TaskOrchestrationContext context,
        ClaimProcessingInput input,
        ILogger logger)
    {
        var claimId = input.ClaimId;

        await context.CallActivityAsync(nameof(ClaimActivities.MarkValidatingActivity), claimId, Retryable);

        //1 Client Registry
        var client = await context.CallActivityAsync<ClientValidationActivityResult>(
            nameof(ClaimActivities.VerifyClientActivity), claimId, Retryable
        );

        if (!client.IsValid)
        {
            await Reject(context, claimId, client.FailureReason ?? "Client validation failed");
            return;
        }

        //2 Policy manager (validates and on failure, rejects the claim inside the activity)
        var policyValid = await context.CallActivityAsync<bool>(
            nameof(ClaimActivities.VerifyPolicyActivity),
            new ValidatePolicyActivityInput {ClaimId = claimId, ClientId = client.ClientId!},
            Retryable
        );

        if (!policyValid) return;

        //3 Approve
        await context.CallActivityAsync(nameof(ClaimActivities.ApproveClaimActivity), claimId, Retryable);

        //4 Payment System - initiate. Deliberately not retried: a failure after the provider
        // took the instruction would otherwise pay the claim twice.
        var initiation = await context.CallActivityAsync<PaymentInitiationActivityResult>
        (
            nameof(ClaimActivities.RequestPaymentActivity),
            new InitiatePaymentActivityInput
            {
                ClaimId = claimId,
                CallbackUrl = input.PaymentCallbackUrl
            });
        if (!initiation.Accepted)return; //activity already recorded payment failed

        //5 Await the synchronous completion callback, bounded by a payment timeout 
        using var paymentCts = new CancellationTokenSource();
        var completionEvent = context.WaitForExternalEvent<PaymentCompletionNotification>(PaymentCompletedEventName);
        var paymentTimeout = context.CreateTimer(
            context.CurrentUtcDateTime.Add(input.PaymentWaitTimeout),paymentCts.Token
        );

        var completed = await Task.WhenAny(completionEvent, paymentTimeout);
        if (completed == paymentTimeout)
        {
            await context.CallActivityAsync(nameof(ClaimActivities.FailClaimActivity),
            new RejectClaimsActivityInput
            {
                ClaimId= claimId,
                Reason = $"Payment completion not received within {input.PaymentWaitTimeout}"
            },
            Retryable);
            logger.LogInformation("Payment timed out for claim {ClaimId}", claimId);

            return;
        }

        // The callback arrived, so stop the timer rather than leaving it pending.
        paymentCts.Cancel();

        var notification = await completionEvent;

        var succeeded = await context.CallActivityAsync<bool>(nameof(
            ClaimActivities.HandlePaymentCompletionActivity),
            new PaymentCompletionActivityInput
            {
                ClaimId = claimId,
                Notification = notification
            },
            Retryable);
        if (!succeeded) return;

        //6 Complete
        await context.CallActivityAsync(nameof(ClaimActivities.CompleteClaimActivity), claimId, Retryable);
        logger.LogInformation("Claim {ClaimId} completed successfully", claimId);
    }

    private static Task Reject(TaskOrchestrationContext context, Guid claimId, string reason)
    {
        return context.CallActivityAsync(nameof(ClaimActivities.RejectClaimActivity), new 
        RejectClaimsActivityInput
        {
            ClaimId = claimId,
            Reason = reason
        },
        Retryable);
    }
}
