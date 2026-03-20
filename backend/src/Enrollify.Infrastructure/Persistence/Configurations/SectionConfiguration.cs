using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.GradeLevel).HasMaxLength(50).IsRequired();
        builder.Property(s => s.SchoolYear).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Adviser).HasMaxLength(200);

        builder.HasIndex(s => new { s.TenantId, s.SchoolYear, s.GradeLevel });

        builder.Ignore(s => s.CurrentCount);
        builder.Ignore(s => s.IsFull);
    }
}
