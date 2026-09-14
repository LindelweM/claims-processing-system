using Claims.Contracts.Enums;
using Claims.Domain;
using Microsoft.Data.SqlClient;
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
    public Task<Claim?> GetByChannelReferenceAsync(
        string channel,
        string channelReference,
        CancellationToken cancellationToken = default) =>
        dbContext.Claims
            .Include(c => c.History)
            .FirstOrDefaultAsync(
                c => c.Channel == channel && c.ChannelReference == channelReference,
                cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TryAddAsync(Claim claim, CancellationToken cancellationToken = default)
    {
        await dbContext.Claims.AddAsync(claim, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (claim.ChannelReference is not null && IsUniqueViolation(ex))
        {
            // Stop tracking the rejected claim, so a later save on this context does not retry it.
            dbContext.Entry(claim).State = EntityState.Detached;
            foreach (var entry in claim.History)
            {
                dbContext.Entry(entry).State = EntityState.Detached;
            }

            return false;
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(Claim claim, CancellationToken cancellationToken = default) =>
        await dbContext.Claims.AddAsync(claim, cancellationToken);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    /// <summary>True when SQL Server refused the write for a duplicate key (errors 2601 and 2627).</summary>
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
