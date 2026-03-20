using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class WorkflowDefinition : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
}
