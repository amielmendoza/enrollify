namespace Enrollify.Application.DTOs.RequirementTemplates;

public record RequirementTemplateDto(
    Guid Id,
    string DocumentName,
    string? GradeLevel,
    bool IsActive,
    int DisplayOrder,
    bool KeepOnFile = false);

public record CreateRequirementTemplateRequest(
    string DocumentName,
    string? GradeLevel,
    int DisplayOrder = 0,
    bool KeepOnFile = false);

public record UpdateRequirementTemplateRequest(
    string DocumentName,
    string? GradeLevel,
    bool IsActive,
    int DisplayOrder,
    bool KeepOnFile = false);
