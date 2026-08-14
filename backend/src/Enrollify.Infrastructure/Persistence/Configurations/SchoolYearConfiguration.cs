using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class SchoolYearConfiguration : IEntityTypeConfiguration<SchoolYear>
{
    public void Configure(EntityTypeBuilder<SchoolYear> builder)
    {
        builder.ToTable("SchoolYears");
        builder.HasKey(sy => sy.Id);

        builder.Property(sy => sy.Name).HasMaxLength(20).IsRequired();
        builder.Property(sy => sy.IsActive).HasDefaultValue(false);

        builder.HasIndex(sy => new { sy.TenantId, sy.Name }).IsUnique();
    }
}
