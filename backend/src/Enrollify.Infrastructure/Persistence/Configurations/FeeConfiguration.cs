using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class FeeConfiguration : IEntityTypeConfiguration<Fee>
{
    public void Configure(EntityTypeBuilder<Fee> builder)
    {
        builder.ToTable("Fees");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.Property(f => f.Description).HasMaxLength(500);
        builder.Property(f => f.Amount).HasPrecision(18, 2);
        builder.Property(f => f.SchoolYear).HasMaxLength(20).IsRequired();
        builder.Property(f => f.GradeLevel).HasMaxLength(50).IsRequired();

        builder.HasIndex(f => new { f.TenantId, f.SchoolYear, f.GradeLevel });
    }
}
