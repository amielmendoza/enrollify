using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class EnrollmentFeeConfiguration : IEntityTypeConfiguration<EnrollmentFee>
{
    public void Configure(EntityTypeBuilder<EnrollmentFee> builder)
    {
        builder.ToTable("EnrollmentFees");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(500);
        builder.Property(f => f.Amount).HasPrecision(18, 2);

        builder.HasOne(f => f.Enrollment)
            .WithMany(e => e.FeeSnapshot)
            .HasForeignKey(f => f.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.EnrollmentId);
    }
}
