using Claims.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Claims.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Claim"/> aggregate to the Claims table.
/// </summary>
public sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.ClaimReference)
            .HasMaxLength(32)
            .IsRequired();

        // The reference is quoted to claimants, so duplicates must be impossible: the random
        // suffix in Claim.BuildReference makes collisions unlikely, not prevented.
        builder.HasIndex(c => c.ClaimReference)
            .IsUnique();

        builder.Property(c => c.Channel)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.ChannelReference)
            .HasMaxLength(100);

        // A channel that resends a submission must get the original claim back rather than a
        // second one that could also pay out. Submissions without a reference are not covered.
        builder.HasIndex(c => new { c.Channel, c.ChannelReference })
            .IsUnique()
            .HasFilter("[ChannelReference] IS NOT NULL");

        // Enums are stored as text so the table stays readable to anyone auditing a claim,
        // and so reordering the enum cannot silently remap existing rows.
        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(c => c.Priority)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(c => c.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(c => c.PolicyNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.PolicyholderIdNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.ClientId)
            .HasMaxLength(64);

        builder.Property(c => c.ClaimantFirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.ClaimantLastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.ClaimantIdNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.AccountHolder)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.AccountNumber)
            .HasMaxLength(34)
            .IsRequired();

        builder.Property(c => c.BranchCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.BankName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.IncidentDate)
            .HasColumnType("date");

        builder.Property(c => c.ClaimAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.ApprovedAmount)
            .HasPrecision(18, 2);

        builder.Property(c => c.Currency)
            .HasMaxLength(3)
            .IsFixedLength()
            .IsRequired();

        builder.Property(c => c.PaymentReference)
            .HasMaxLength(64);

        builder.Property(c => c.FailureReason)
            .HasMaxLength(1000);

        // Work queues are driven off open claims ordered by deadline.
        builder.HasIndex(c => new { c.Status, c.Deadline });

        builder.HasIndex(c => c.PolicyNumber);

        builder.HasMany(c => c.History)
            .WithOne()
            .HasForeignKey(h => h.ClaimId)
            .OnDelete(DeleteBehavior.Cascade);

        // History is exposed as a read-only view over the _history backing field, so EF must
        // populate the field rather than going through the property.
        builder.Metadata
            .FindNavigation(nameof(Claim.History))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
