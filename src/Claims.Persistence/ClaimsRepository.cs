using Claims.Contracts.Enums;
using Claims.Domain;
using Microsoft.EntityFrameworkCore;

namespace Claims.Persistence;

/// <summary>
/// Entity Framework implementation of <see cref="IClaimRepository"/>.
/// </summary>
public sealed class ClaimsRepository : IClaimRepository
{
    private readonly ClaimsDbContext dbContext;

    /// <summary>Creates a repository over the given context.</summary>
    /// <param name="dbContext">Context the claims are read from and written to.</param>
    public ClaimsRepository(ClaimsDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<Claim?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Claims
            .Include(c => c.History)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Claim?> GetByReferenceAsync(
        string claimReference,
        CancellationToken cancellationToken = default) =>
        dbContext.Claims
            .Include(c => c.History)
            .FirstOrDefaultAsync(c => c.ClaimReference == claimReference, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Claim claim, CancellationToken cancellationToken = default) =>
        await dbContext.Claims.AddAsync(claim, cancellationToken);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
