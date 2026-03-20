using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Enrollify.Infrastructure.Persistence.Configurations;

public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
{
    public void Configure(EntityTypeBuilder<WorkflowStep> builder)
    {
        builder.ToTable("WorkflowSteps");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.StepName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.FromStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.ToStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.RequiredRole).HasMaxLength(50);

        builder.HasOne(s => s.WorkflowDefinition)
            .WithMany(w => w.Steps)
            .HasForeignKey(s => s.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.WorkflowDefinitionId, s.StepOrder });
    }
}
