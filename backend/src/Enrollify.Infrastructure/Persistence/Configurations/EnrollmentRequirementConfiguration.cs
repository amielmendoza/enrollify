using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class EnrollmentRequirementConfiguration : IEntityTypeConfiguration<EnrollmentRequirement>
{
    public void Configure(EntityTypeBuilder<EnrollmentRequirement> builder)
    {
        builder.ToTable("EnrollmentRequirements");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.DocumentName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.FileName).HasMaxLength(500);
        builder.Property(r => r.Notes).HasMaxLength(1000);
        builder.Property(r => r.VerifiedBy).HasMaxLength(200);

        builder.HasOne(r => r.Enrollment)
            .WithMany(e => e.Requirements)
            .HasForeignKey(r => r.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.EnrollmentId);
    }
}
