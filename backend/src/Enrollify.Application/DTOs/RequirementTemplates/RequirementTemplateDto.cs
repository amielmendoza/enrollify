namespace Enrollify.Application.DTOs.RequirementTemplates;

public record RequirementTemplateDto(
    Guid Id,
    string DocumentName,
    string? GradeLevel,
    bool IsActive,
    int DisplayOrder);

public record CreateRequirementTemplateRequest(
    string DocumentName,
    string? GradeLevel,
    int DisplayOrder = 0);

public record UpdateRequirementTemplateRequest(
    string DocumentName,
    string? GradeLevel,
    bool IsActive,
    int DisplayOrder);
