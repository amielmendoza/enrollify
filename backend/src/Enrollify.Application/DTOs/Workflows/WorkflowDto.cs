using Enrollify.Domain.Enums;

namespace Enrollify.Application.DTOs.Workflows;

public record WorkflowDefinitionDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsActive,
    List<WorkflowStepDto> Steps);

public record WorkflowStepDto(
    Guid Id,
    int StepOrder,
    string StepName,
    EnrollmentStatus FromStatus,
    EnrollmentStatus ToStatus,
    string? RequiredRole,
    bool RequiresApproval);

public record CreateWorkflowRequest(
    string Name,
    string? Description,
    List<CreateWorkflowStepRequest> Steps);

public record CreateWorkflowStepRequest(
    int StepOrder,
    string StepName,
    EnrollmentStatus FromStatus,
    EnrollmentStatus ToStatus,
    string? RequiredRole,
    bool RequiresApproval);
