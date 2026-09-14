using System.Data.Common;
using Claims.Contracts.Integration;
using Claims.Integration.ClientRegistry;
using Microsoft.Extensions.Logging;

namespace Claims.Integration.Stubs;

/// <summary>
/// In-memory stand-in for the client registry, for local runs and tests.
/// </summary>
/// <remarks>
/// Matches every claimant whose identity number is non-empty and returns a client id derived
/// from it, so the same claimant resolves to the same client across calls.
/// </remarks>
public sealed class StubClientRegistryClient : IClientRegistryClient
{
    private readonly ILogger<StubClientRegistryClient> logger;

    /// <summary>Creates the stub registry.</summary>
    /// <param name="logger">Log recording that a stub answered.</param>
    public StubClientRegistryClient(ILogger<StubClientRegistryClient> logger) => this.logger = logger;

    /// <inheritdoc />
    public Task<ClientValidationResult> ValidateAsync(
        ClientValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Stub client registry answering for claim {ClaimId} id {IdNumber}", request.ClaimId, request.IdNumber);

        var result = string.IsNullOrWhiteSpace(request.IdNumber) || 
        request.IdNumber.EndsWith("0000", StringComparison.Ordinal)
            ? ClientValidationResult.Invalid("No identity number supplied.")
            : ClientValidationResult.Valid($"STUB-CLIENT-{request.IdNumber}");

        return Task.FromResult(result);
    }
}
