using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;

namespace Enrollify.Application.Features.Workflows;

/// <summary>
/// Single source of truth for the standard enrollment workflow. Used to seed new tenants,
/// backfill existing tenants that have none, and as the runtime fallback in
/// MoveEnrollmentStepCommand so status transitions never dead-end on a tenant
/// without an active WorkflowDefinition.
/// </summary>
public static class DefaultWorkflow
{
    public const string DefaultName = "Standard Enrollment Workflow";

    public static readonly (int Order, string StepName, EnrollmentStatus From, EnrollmentStatus To, string Role, bool RequiresApproval)[] Steps =
    {
        (1, "Submit Application",   EnrollmentStatus.Draft,     EnrollmentStatus.Submitted, "Registrar", false),
        (2, "Assess Requirements",  EnrollmentStatus.Submitted, EnrollmentStatus.Assessed,  "Registrar", true),
        (3, "Approve Enrollment",   EnrollmentStatus.Assessed,  EnrollmentStatus.Approved,  "Admin",     true),
        (4, "Confirm Payment",      EnrollmentStatus.Approved,  EnrollmentStatus.Paid,      "Registrar", false),
        (5, "Finalize Enrollment",  EnrollmentStatus.Paid,      EnrollmentStatus.Enrolled,  "Registrar", false),
    };

    public static EnrollmentStatus? NextStatus(EnrollmentStatus from)
    {
        foreach (var step in Steps)
            if (step.From == from)
                return step.To;
        return null;
    }

    public static WorkflowDefinition Build(Guid tenantId)
    {
        var workflow = new WorkflowDefinition
        {
            TenantId = tenantId,
            Name = DefaultName,
            Description = "Default enrollment workflow for K-12",
            IsActive = true
        };

        foreach (var step in Steps)
        {
            workflow.Steps.Add(new WorkflowStep
            {
                TenantId = tenantId,
                StepOrder = step.Order,
                StepName = step.StepName,
                FromStatus = step.From,
                ToStatus = step.To,
                RequiredRole = step.Role,
                RequiresApproval = step.RequiresApproval
            });
        }

        return workflow;
    }
}
