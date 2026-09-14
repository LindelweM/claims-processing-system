using System.Globalization;
using System.Text;
using System.Text.Json;
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
/// simulated completion callback, signed the way a real provider would sign it, so the webhook
/// that handles a real provider's notification can be exercised end to end.
/// </remarks>
public sealed class StubPaymentClient : IPaymentClient
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentOptions _options;
    private readonly PaymentCallbackOptions _callbackOptions;
    private readonly ILogger<StubPaymentClient> _logger;

    /// <summary>Creates the stub payment provider.</summary>
    /// <param name="httpClientFactory">Factory for the client that posts completion callbacks.</param>
    /// <param name="options">Controls how the stub settles the payments it accepts.</param>
    /// <param name="callbackOptions">Holds the secret callbacks are signed with.</param>
    /// <param name="logger">Log recording that a stub answered.</param>
    public StubPaymentClient(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<PaymentOptions> options,
        IOptions<PaymentCallbackOptions> callbackOptions,
        ILogger<StubPaymentClient> logger)
    {
        this._httpClientFactory = httpClientFactory;
        this._options = options.CurrentValue;
        this._callbackOptions = callbackOptions.Value;
        this._logger = logger;
    }

    /// <inheritdoc />
    public Task<PaymentInstructionResult> RequestPaymentAsync(
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

            return Task.FromResult(PaymentInstructionResult.Rejected("Valid Amount and currency must be supplied."));
        }

        var willSucceed = !request.AccountNumber.EndsWith("9999", StringComparison.Ordinal);

        if (_options.AutoCompletePayments && !string.IsNullOrWhiteSpace(request.CallbackUrl))
        {
            // Deliberately not awaited: a real provider answers the instruction first and calls back later.
            _ = SendCompletionNotificationAsync(request, paymentReference, willSucceed);
        }

        return Task.FromResult(PaymentInstructionResult.Accepted(paymentReference));
    }

    /// <summary>
    /// Waits out the configured settlement delay, then posts a signed completion callback.
    /// </summary>
    private async Task SendCompletionNotificationAsync(
        PaymentRequest request,
        string paymentReference,
        bool willSucceed)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.AutoCompleteDelaySeconds)));

            var notification = new PaymentCompletionNotification
            {
                PaymentReference = paymentReference,
                ClaimId = request.ClaimId,
                Status = willSucceed ? PaymentStatus.Succeeded : PaymentStatus.Failed,
                SettledAt = DateTimeOffset.UtcNow,
                FailureReason = willSucceed ? null : "Simulated failure",
            };

            var body = JsonSerializer.Serialize(notification, JsonSerializerOptions);
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            using var message = new HttpRequestMessage(HttpMethod.Post, request.CallbackUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            message.Headers.Add(PaymentCallbackSignature.TimestampHeader, timestamp.ToString(CultureInfo.InvariantCulture));
            message.Headers.Add(
                PaymentCallbackSignature.SignatureHeader,
                PaymentCallbackSignature.Compute(_callbackOptions.CallbackSigningSecret, timestamp, body));

            _logger.LogInformation(
                "Stub payment provider sending simulated completion callback for payment {PaymentReference} with status {Status}",
                paymentReference,
                notification.Status);

            using var response = await _httpClientFactory.CreateClient().SendAsync(message);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Stub payment provider's completion callback for payment {PaymentReference} was answered {StatusCode}",
                    paymentReference,
                    response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // This runs detached from any request, so a fault here must not reach the host.
            _logger.LogError(
                ex,
                "Simulated completion callback for payment {PaymentReference} failed",
                paymentReference);
        }
    }
}
