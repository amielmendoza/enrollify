using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class ApplicationFormFieldConfiguration : IEntityTypeConfiguration<ApplicationFormField>
{
    public void Configure(EntityTypeBuilder<ApplicationFormField> builder)
    {
        builder.ToTable("ApplicationFormFields");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FieldKey).HasMaxLength(60).IsRequired();
        builder.Property(f => f.Label).HasMaxLength(150).IsRequired();
        builder.Property(f => f.FieldType).HasMaxLength(20).IsRequired();
        builder.Property(f => f.Section).HasMaxLength(20).IsRequired();
        builder.Property(f => f.AppliesTo).HasMaxLength(20).IsRequired();
        builder.Property(f => f.Options).HasMaxLength(2000);
        builder.Property(f => f.HelpText).HasMaxLength(500);

        builder.HasIndex(f => new { f.TenantId, f.FieldKey }).IsUnique();
        builder.HasIndex(f => new { f.TenantId, f.Section, f.DisplayOrder });
    }
}
