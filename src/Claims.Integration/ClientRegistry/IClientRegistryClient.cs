using Claims.Contracts.Integration;

namespace Claims.Integration.ClientRegistry;

/// <summary>
/// Calls the client registry to resolve and verify the party lodging a claim.
/// </summary>
public interface IClientRegistryClient
{
    /// <summary>Asks the registry to match and verify the claimant.</summary>
    /// <param name="request">Claimant details captured at intake.</param>
    /// <param name="cancellationToken">Token to cancel the call.</param>
    /// <returns>
    /// The registry's verdict. A call the registry answers with a refusal returns an invalid
    /// result rather than throwing; only transport and protocol faults throw.
    /// </returns>
    Task<ClientValidationResult> ValidateAsync(
        ClientValidationRequest request,
        CancellationToken cancellationToken = default);
}
