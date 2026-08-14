using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class LedgerAdjustmentConfiguration : IEntityTypeConfiguration<LedgerAdjustment>
{
    public void Configure(EntityTypeBuilder<LedgerAdjustment> builder)
    {
        builder.ToTable("LedgerAdjustments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Amount).HasPrecision(18, 2);
        builder.Property(a => a.PostedBy).HasMaxLength(200).IsRequired();
        builder.Property(a => a.VoidedBy).HasMaxLength(200);
        builder.Property(a => a.VoidReason).HasMaxLength(300);

        builder.HasOne(a => a.Enrollment)
            .WithMany(e => e.LedgerAdjustments)
            .HasForeignKey(a => a.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.EnrollmentId);
    }
}
