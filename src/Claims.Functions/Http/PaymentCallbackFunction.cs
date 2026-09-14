using Claims.Contracts.Enums;
using Claims.Contracts.Integration;
using Claims.Functions.Orchestrations;
using Claims.Integration.Payments;
using Claims.Persistence;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Net;

namespace Claims.Functions.Http;

/// <summary>
/// Receives the payment provider's settlement callback and hands it to the claim's orchestration.
/// </summary>
public sealed class PaymentCallbackFunction
{

    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly IClaimRepository _claims;
    private readonly PaymentCallbackOptions _callbackOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PaymentCallbackFunction> _logger;

    public PaymentCallbackFunction(
        IClaimRepository claims,
        IOptions<PaymentCallbackOptions> callbackOptions,
        TimeProvider timeProvider,
        ILogger<PaymentCallbackFunction> logger)
    {
        _claims = claims;
        _callbackOptions = callbackOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Handles a settlement callback for one claim.</summary>
    /// <param name="request">Incoming HTTP request carrying the callback.</param>
    /// <param name="claimId">Claim the payment settles, taken from the route.</param>
    /// <param name="durableClient">Client used to wake the claim's orchestration.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <returns>Accepted once the orchestration has been notified.</returns>
    /// <remarks>
    /// Anonymous at the host because the provider cannot be issued a function key without
    /// putting it in a URL. Callers are authenticated by the HMAC signature instead, checked
    /// before the body is trusted for anything.
    /// </remarks>
    [Function("PaymentCallback")]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "claims/{claimId:guid}/payment-callback")]
        HttpRequestData request,
        Guid claimId,
        [DurableClient] DurableTaskClient durableClient,
        CancellationToken cancellationToken)
    {
        var body = await new StreamReader(request.Body).ReadToEndAsync(cancellationToken);

        var signatureValid = PaymentCallbackSignature.IsValid(
            _callbackOptions.CallbackSigningSecret,
            Header(request, PaymentCallbackSignature.TimestampHeader),
            Header(request, PaymentCallbackSignature.SignatureHeader),
            body,
            _timeProvider.GetUtcNow(),
            _callbackOptions.SignatureTolerance);

        if (!signatureValid)
        {
            _logger.LogWarning("Payment callback for claim {ClaimId} had a missing, invalid or expired signature", claimId);
            return await Json(request, HttpStatusCode.Unauthorized, new{ error = "Callback signature is missing, invalid or expired" });
        }

        PaymentCompletionNotification? notification;
        try
        {
            notification = JsonSerializer.Deserialize<PaymentCompletionNotification>(body, JsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Payment callback for claim {ClaimId} could not be deserialized", claimId);
            return await Json(request, HttpStatusCode.BadRequest, new{ error = $"Could not deserialize request body" });
        }
       
        if (notification is null)
        {
            _logger.LogWarning("Payment callback for claim {ClaimId} was empty", claimId);
            return await Json(request, HttpStatusCode.BadRequest, new{ error = $"Request body was empty" });
        }

        var claim = await _claims.GetByIdAsync(claimId, cancellationToken);

        if (claim is null)
        {
            _logger.LogWarning("Payment callback for unknown claim {ClaimId}", claimId);
            return await Json(request, HttpStatusCode.NotFound, new{ error = $"No claim found with ID {claimId}" });
        }

        // Only a callback quoting the payment this claim instructed may settle it.
        if (notification.ClaimId != claimId || notification.PaymentReference != claim.PaymentReference)
        {
            _logger.LogWarning(
                "Payment callback for claim {ClaimId} quoted payment {PaymentReference}, which does not match the claim",
                claimId,
                notification.PaymentReference);
            return await Json(request, HttpStatusCode.BadRequest, new{ error = "Callback does not match the payment instructed for this claim" });
        }

        var metadata = await durableClient.GetInstanceAsync(claimId.ToString(), cancellationToken);

        if (metadata is { IsRunning: true })
        {
            await durableClient.RaiseEventAsync(claimId.ToString(),
            ClaimProcessingOrchestrator.PaymentCompletedEventName, notification, cancellationToken);

            _logger.LogInformation("Raised payment completion for claim {ClaimId}", claimId);
            return await Json(request, HttpStatusCode.Accepted, new {accepted = true});
        }

        // The orchestration has already ended, typically because the payment wait timed out.
        // Money may still have moved, so the outcome is applied to the claim directly rather
        // than raised as an event nothing is listening for.
        if (claim.PaymentStatus != PaymentStatus.Pending)
        {
            _logger.LogInformation("Duplicate payment callback for claim {ClaimId} ignored", claimId);
            return await Json(request, HttpStatusCode.OK, new {accepted = true, duplicate = true});
        }

        var now = DateTimeOffset.UtcNow;
        claim.AddHistory(claim.Status, now, "Settlement callback arrived after processing had ended");

        if (notification.Status == PaymentStatus.Succeeded)
        {
            claim.MarkPaid(notification.SettledAt ?? now);
            claim.Completed(now);
        }
        else
        {
            claim.MarkPaymentFailed(notification.FailureReason ?? "Payment did not settle.", notification.SettledAt ?? now);
        }

        await _claims.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Late payment callback for claim {ClaimId} applied with status {Status}",
            claimId,
            notification.Status);
        return await Json(request, HttpStatusCode.OK, new {accepted = true, reconciled = true});
    }
    private static string? Header(HttpRequestData request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static async Task<HttpResponseData> Json(HttpRequestData request, HttpStatusCode statusCode, object payload)
    {
        var response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(payload, JsonSerializerOptions));
        return response;
    }
}
