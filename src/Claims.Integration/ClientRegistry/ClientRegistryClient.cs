using System.Net;
using System.Net.Http.Json;
using Claims.Contracts.Integration;
using Microsoft.Extensions.Logging;

namespace Claims.Integration.ClientRegistry;

/// <summary>
/// HTTP implementation of <see cref="IClientRegistryClient"/>.
/// </summary>
public sealed class ClientRegistryClient : IClientRegistryClient
{
    private const string ValidatePath = "clients/validate";

    private readonly HttpClient httpClient;
    private readonly ILogger<ClientRegistryClient> logger;

    /// <summary>Creates a client over the configured registry endpoint.</summary>
    /// <param name="httpClient">Client addressed at the registry.</param>
    /// <param name="logger">Log for recording refusals.</param>
    public ClientRegistryClient(HttpClient httpClient, ILogger<ClientRegistryClient> logger)
    {
        this.httpClient = httpClient;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<ClientValidationResult> ValidateAsync(
        ClientValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(ValidatePath, request, cancellationToken);

        // A claimant the registry cannot match is a business outcome, not a transport fault,
        // so it comes back as an invalid result the orchestration can act on.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
        {
            logger.LogInformation(
                "Client registry did not match claim {ClaimId} ({StatusCode})",
                request.ClaimId,
                response.StatusCode);

            return ClientValidationResult.Invalid($"Client not found on registry ({response.StatusCode}).");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ClientValidationResult>(cancellationToken)
            ?? ClientValidationResult.Invalid("Client registry returned an empty response.");
    }
}
