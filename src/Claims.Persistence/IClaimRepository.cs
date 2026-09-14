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

    /// <summary>Loads the claim a channel already lodged under its own reference.</summary>
    /// <param name="channel">Channel the claim was lodged through.</param>
    /// <param name="channelReference">The channel's reference for the submission.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The claim, or null when the channel has not lodged that reference.</returns>
    Task<Claim?> GetByChannelReferenceAsync(
        string channel,
        string channelReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds and saves a newly lodged claim, unless the channel has already lodged a claim under
    /// the same reference.
    /// </summary>
    /// <param name="claim">Claim to add.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// True when the claim was saved; false when a claim with the same channel reference already
    /// exists, typically because a concurrent resubmission saved first.
    /// </returns>
    Task<bool> TryAddAsync(Claim claim, CancellationToken cancellationToken = default);

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
