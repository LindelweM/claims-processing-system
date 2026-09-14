using Claims.Domain;

namespace Claims.Persistence;

/// <summary>
/// Reads and writes the <see cref="Claim"/> aggregate, including its audit trail.
/// </summary>
public interface IClaimRepository
{
    /// <summary>Loads a claim and its history by identifier.</summary>
    /// <param name="id">Identifier of the claim.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The claim, or null when no claim has that identifier.</returns>
    Task<Claim?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Loads a claim and its history by the reference quoted to the claimant.</summary>
    /// <param name="claimReference">Reference quoted to the claimant.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The claim, or null when no claim has that reference.</returns>
    Task<Claim?> GetByReferenceAsync(string claimReference, CancellationToken cancellationToken = default);

    /// <summary>Adds a newly lodged claim to the store.</summary>
    /// <param name="claim">Claim to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task AddAsync(Claim claim, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists pending changes, writing the claim and any audit entries it gained in one
    /// transaction.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
