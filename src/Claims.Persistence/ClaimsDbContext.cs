using Claims.Domain;
using Microsoft.EntityFrameworkCore;

namespace Claims.Persistence;

/// <summary>
/// Entity Framework context for the claims store.
/// </summary>
/// <param name="options">Provider and connection options for the context.</param>
public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options) : DbContext(options)
{
    /// <summary>Claims lodged against policies.</summary>
    public DbSet<Claim> Claims => Set<Claim>();

    /// <summary>
    /// Audit trail entries. Exposed for querying only; entries are written through the
    /// <see cref="Claim"/> aggregate, which owns them.
    /// </summary>
    public DbSet<ClaimStatusHistory> ClaimStatusHistory => Set<ClaimStatusHistory>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClaimsDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
