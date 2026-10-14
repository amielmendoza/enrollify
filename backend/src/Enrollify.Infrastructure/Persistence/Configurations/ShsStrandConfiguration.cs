using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class ShsStrandConfiguration : IEntityTypeConfiguration<ShsStrand>
{
    public void Configure(EntityTypeBuilder<ShsStrand> builder)
    {
        builder.ToTable("ShsStrands");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Track).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Strand).HasMaxLength(100).IsRequired();
        builder.HasIndex(s => new { s.TenantId, s.Track, s.Strand }).IsUnique();
        builder.HasIndex(s => new { s.TenantId, s.IsActive, s.DisplayOrder });
    }
}
