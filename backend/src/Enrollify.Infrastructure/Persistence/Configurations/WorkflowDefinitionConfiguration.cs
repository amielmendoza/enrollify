using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Description).HasMaxLength(500);

        builder.HasIndex(w => w.TenantId);
    }
}
