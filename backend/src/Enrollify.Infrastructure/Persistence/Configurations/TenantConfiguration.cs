using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Subdomain).HasMaxLength(100).IsRequired();
        builder.Property(t => t.ContactEmail).HasMaxLength(200);
        builder.Property(t => t.ContactPhone).HasMaxLength(50);
        builder.Property(t => t.Address).HasMaxLength(500);

        builder.HasIndex(t => t.Subdomain).IsUnique();
    }
}
