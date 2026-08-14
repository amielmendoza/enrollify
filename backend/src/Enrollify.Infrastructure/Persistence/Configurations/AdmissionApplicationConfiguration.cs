using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class AdmissionApplicationConfiguration : IEntityTypeConfiguration<AdmissionApplication>
{
    public void Configure(EntityTypeBuilder<AdmissionApplication> builder)
    {
        builder.ToTable("AdmissionApplications");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ApplicationNumber).HasMaxLength(30).IsRequired();
        builder.Property(a => a.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.MiddleName).HasMaxLength(100);
        builder.Property(a => a.LastName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(200).IsRequired();
        builder.Property(a => a.ContactNumber).HasMaxLength(50);
        builder.Property(a => a.Gender).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Address).HasMaxLength(500);
        builder.Property(a => a.GradeLevel).HasMaxLength(50).IsRequired();
        builder.Property(a => a.SchoolYear).HasMaxLength(20).IsRequired();
        builder.Property(a => a.PreviousSchool).HasMaxLength(200);
        builder.Property(a => a.PreviousSchoolAddress).HasMaxLength(500);
        builder.Property(a => a.GuardianName).HasMaxLength(200);
        builder.Property(a => a.GuardianContact).HasMaxLength(50);
        builder.Property(a => a.GuardianRelationship).HasMaxLength(50);
        builder.Property(a => a.Status).HasMaxLength(30).IsRequired();
        builder.Property(a => a.ReviewedBy).HasMaxLength(200);
        builder.Property(a => a.ReviewNotes).HasMaxLength(1000);

        builder.Property(a => a.ApplicationType).HasMaxLength(20).IsRequired();
        builder.Property(a => a.ParentFirstName).HasMaxLength(100);
        builder.Property(a => a.ParentLastName).HasMaxLength(100);
        builder.Property(a => a.ParentEmail).HasMaxLength(200);
        builder.Property(a => a.ParentContactNumber).HasMaxLength(50);
        builder.Property(a => a.ParentUserId);
        builder.Property(a => a.CustomFieldValues).HasColumnType("nvarchar(max)");
        builder.HasIndex(a => new { a.TenantId, a.ApplicationNumber }).IsUnique();
        builder.HasIndex(a => new { a.TenantId, a.Status });
        builder.HasIndex(a => new { a.TenantId, a.ParentUserId });
    }
}
