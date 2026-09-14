using System.Net;
using System.Net.Http.Json;
using Claims.Contracts.Integration;
using Microsoft.Extensions.Logging;

namespace Claims.Integration.PolicyManager;

/// <summary>
/// HTTP implementation of <see cref="IPolicyManagerClient"/>.
/// </summary>
public sealed class PolicyManagerClient : IPolicyManagerClient
{
    private const string VerifyPath = "policies/verify";

    private readonly HttpClient httpClient;
    private readonly ILogger<PolicyManagerClient> logger;

    /// <summary>Creates a client over the configured policy manager endpoint.</summary>
    /// <param name="httpClient">Client addressed at the policy manager.</param>
    /// <param name="logger">Log for recording declines.</param>
    public PolicyManagerClient(HttpClient httpClient, ILogger<PolicyManagerClient> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<PolicyVerificationResponse> VerifyAsync(
        PolicyVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(VerifyPath, request, cancellationToken);

        // An unknown policy is an answer about cover, not a fault, so it is returned as a
        // decline the orchestration can reject the claim on.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        {
            logger.LogInformation(
                "Policy manager declined claim {ClaimId} on policy {PolicyNumber} ({StatusCode})",
                request.ClaimId,
                request.PolicyNumber,
                response.StatusCode);

            return PolicyVerificationResponse.Declined(
                $"Policy {request.PolicyNumber} not found or not covered ({response.StatusCode}).");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PolicyVerificationResponse>(cancellationToken)
            ?? PolicyVerificationResponse.Declined("Policy manager returned an empty response.");
    }
}
