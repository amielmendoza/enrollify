using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class EnrollmentStatusHistoryConfiguration : IEntityTypeConfiguration<EnrollmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<EnrollmentStatusHistory> builder)
    {
        builder.ToTable("EnrollmentStatusHistories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.Remarks).HasMaxLength(1000);

        builder.HasOne(h => h.Enrollment)
            .WithMany(e => e.StatusHistory)
            .HasForeignKey(h => h.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(h => h.EnrollmentId);
    }
}
