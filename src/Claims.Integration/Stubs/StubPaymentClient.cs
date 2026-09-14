using System.Net.Http.Json;
using Claims.Contracts.Enums;
using Claims.Contracts.Integration;
using Claims.Integration.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Claims.Integration.Stubs;

/// <summary>
/// In-memory stand-in for the payment provider, for local runs and tests.
/// </summary>
/// <remarks>
/// Accepts every instruction and never moves money. An accepted instruction is followed by a
/// simulated completion callback, so the webhook that handles a real provider's notification
/// can be exercised end to end.
/// </remarks>
public sealed class StubPaymentClient : IPaymentClient
{
    /// <summary>Name of the client used to post simulated completion callbacks.</summary>
    public static readonly HttpClient CallbackHttpClientName = new ();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentOptions _options;
    private readonly ILogger<StubPaymentClient> _logger;

    /// <summary>Creates the stub payment provider.</summary>
    /// <param name="httpClientFactory">Factory for the client that posts completion callbacks.</param>
    /// <param name="options">Controls how the stub settles the payments it accepts.</param>
    /// <param name="logger">Log recording that a stub answered.</param>
    public StubPaymentClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<PaymentOptions> options,
        ILogger<StubPaymentClient> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._options = options.CurrentValue;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentInstructionResult> RequestPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {

        var paymentReference = $"STUB-PAY-{Guid.NewGuid():N}"[..20];

        _logger.LogInformation(
            "Stub payment provider accepting claim {ClaimReference} for {Amount:0.00} {Currency} as {PaymentReference}",
            request.ClaimReference,
            request.Amount,
            request.Currency,
            paymentReference);

        if (request.Amount <= 0 || string.IsNullOrWhiteSpace(request.AccountNumber) || string.IsNullOrWhiteSpace(request.Currency))
        {
            _logger.LogWarning(
                "Stub payment provider refusing claim {ClaimReference} for {Amount:0.00} {Currency}",
                request.ClaimReference,
                request.Amount,
                request.Currency);

            return await Task.FromResult(PaymentInstructionResult.Rejected("Valid Amount and currency must be supplied."));
        }

        var willSucceed = !request.AccountNumber.EndsWith("9999", StringComparison.Ordinal);

        if (_options.AutoCompletePayments && !string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            _ = ScheduleCompletionNotificationAsync(request, paymentReference, willSucceed);
        }
        return await Task.FromResult(PaymentInstructionResult.Accepted(paymentReference));
    }

    /// <summary>
    /// Starts the simulated settlement callback without waiting for it, the way a real provider
    /// answers the instruction first and calls back later.
    /// </summary>
    private async Task ScheduleCompletionNotificationAsync(
        PaymentRequest request,
        string paymentReference,
        bool willSucceed)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.AutoCompleteDelaySeconds))); // Give the caller a chance to return before we start the callback.
            
            var notification = new PaymentCompletionNotification
            {
                PaymentReference = paymentReference,
                ClaimId = request.ClaimId,
                Status = willSucceed ? PaymentStatus.Succeeded : PaymentStatus.Failed,
                SettledAt = DateTimeOffset.UtcNow,
                FailureReason = willSucceed ? null : "Simulated failure",
            };

            _logger.LogInformation(
                "Stub payment provider sending simulated completion callback for payment {PaymentReference} with status {Status}",
                paymentReference,
                notification.Status);
            
            using var response = await CallbackHttpClientName.PostAsJsonAsync(request.CallbackUrl!, notification);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Stub payment provider failed to send simulated completion callback for payment {PaymentReference}",
                    paymentReference);
            }


        }
        catch (Exception ex)
        {
            // This runs detached from any request, so a fault here must not reach the host.
            _logger.LogError(
                ex,
                "Scheduling simulated completion callback for payment {PaymentReference} failed",
                paymentReference);
        }
    }

}
