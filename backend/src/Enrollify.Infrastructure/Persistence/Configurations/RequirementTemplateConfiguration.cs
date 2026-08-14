using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class RequirementTemplateConfiguration : IEntityTypeConfiguration<RequirementTemplate>
{
    public void Configure(EntityTypeBuilder<RequirementTemplate> builder)
    {
        builder.ToTable("RequirementTemplates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.DocumentName).HasMaxLength(200).IsRequired();
        builder.Property(t => t.GradeLevel).HasMaxLength(50);

        builder.HasIndex(t => new { t.TenantId, t.DocumentName, t.GradeLevel });
    }
}
