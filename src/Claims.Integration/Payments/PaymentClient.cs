using System.Net;
using System.Net.Http.Json;
using Claims.Contracts.Integration;
using Microsoft.Extensions.Logging;

namespace Claims.Integration.Payments;

/// <summary>
/// HTTP implementation of <see cref="IPaymentClient"/>.
/// </summary>
public sealed class PaymentClient : IPaymentClient
{
    private const string PaymentsPath = "payments";

    private readonly HttpClient httpClient;
    private readonly ILogger<PaymentClient> logger;

    /// <summary>Creates a client over the configured payment provider endpoint.</summary>
    /// <param name="httpClient">Client addressed at the payment provider.</param>
    /// <param name="logger">Log for recording refused instructions.</param>
    public PaymentClient(HttpClient httpClient, ILogger<PaymentClient> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentInstructionResult> RequestPaymentAsync(
        PaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(PaymentsPath, request, cancellationToken);

        // A refusal the provider states is an outcome to record against the claim. Anything
        // else is left to throw, so a retry cannot turn an unknown outcome into a second payment.
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            logger.LogWarning(
                "Payment provider refused claim {ClaimReference} ({StatusCode})",
                request.ClaimReference,
                response.StatusCode);

            return PaymentInstructionResult.Rejected(
                $"Payment provider refused the instruction ({response.StatusCode}).");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PaymentInstructionResult>(cancellationToken)
            ?? PaymentInstructionResult.Rejected("Payment provider returned an empty response.");
    }
}
