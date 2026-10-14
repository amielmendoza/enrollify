using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("Enrollments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.SchoolYear).HasMaxLength(20).IsRequired();
        builder.Property(e => e.GradeLevel).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Remarks).HasMaxLength(1000);
        builder.Property(e => e.AssessedTotal).HasPrecision(18, 2);

        builder.Property(e => e.LearnerType).HasMaxLength(20);
        builder.Property(e => e.LastGradeLevelCompleted).HasMaxLength(50);
        builder.Property(e => e.LastSchoolYearCompleted).HasMaxLength(20);
        builder.Property(e => e.LastSchoolAttended).HasMaxLength(200);
        builder.Property(e => e.LastSchoolAddress).HasMaxLength(500);
        builder.Property(e => e.LastSchoolId).HasMaxLength(6);
        builder.Property(e => e.Track).HasMaxLength(100);
        builder.Property(e => e.Strand).HasMaxLength(100);

        builder.HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Section)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.SectionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.TenantId);
        builder.HasIndex(e => new { e.TenantId, e.StudentId, e.SchoolYear });
    }
}
