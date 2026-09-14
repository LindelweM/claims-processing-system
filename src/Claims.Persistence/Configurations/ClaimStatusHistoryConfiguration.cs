using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Persistence.Configurations;

/// <summary>
/// Maps <see cref="ClaimStatusHistory"/> entries to the ClaimStatusHistory table.
/// </summary>
public sealed class ClaimStatusHistoryConfiguration : IEntityTypeConfiguration<ClaimStatusHistory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ClaimStatusHistory> builder)
    {
        builder.ToTable("ClaimStatusHistory");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .ValueGeneratedNever();

        builder.Property(h => h.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(h => h.Detail)
            .HasMaxLength(500);

        // The audit trail is read back in order, per claim.
        builder.HasIndex(h => new { h.ClaimId, h.OccurredAt });
    }
}
