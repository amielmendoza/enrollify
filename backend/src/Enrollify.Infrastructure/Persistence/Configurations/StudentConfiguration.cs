using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.LRN).HasMaxLength(20).IsRequired();
        builder.Property(s => s.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.MiddleName).HasMaxLength(100);
        builder.Property(s => s.LastName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Gender).HasMaxLength(20);
        builder.Property(s => s.Address).HasMaxLength(500).IsRequired();
        builder.Property(s => s.ContactNumber).HasMaxLength(50);
        builder.Property(s => s.Email).HasMaxLength(200);
        builder.Property(s => s.GuardianName).HasMaxLength(200);
        builder.Property(s => s.GuardianContact).HasMaxLength(50);

        builder.Property(s => s.UserId);
        builder.HasIndex(s => new { s.TenantId, s.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL");

        builder.Property(s => s.ParentUserId);
        builder.HasIndex(s => new { s.TenantId, s.ParentUserId });

        builder.HasIndex(s => new { s.TenantId, s.LRN }).IsUnique();
        builder.HasIndex(s => s.TenantId);

        builder.Ignore(s => s.FullName);
    }
}
