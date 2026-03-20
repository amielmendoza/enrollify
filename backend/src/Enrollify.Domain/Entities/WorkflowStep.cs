using Enrollify.Domain.Common;
using Enrollify.Domain.Enums;

namespace Enrollify.Domain.Entities;

public class WorkflowStep : TenantEntity
{
    public Guid WorkflowDefinitionId { get; set; }
    public WorkflowDefinition WorkflowDefinition { get; set; } = default!;

    public int StepOrder { get; set; }
    public string StepName { get; set; } = default!;
    public EnrollmentStatus FromStatus { get; set; }
    public EnrollmentStatus ToStatus { get; set; }
    public string? RequiredRole { get; set; }
    public bool RequiresApproval { get; set; }
}
