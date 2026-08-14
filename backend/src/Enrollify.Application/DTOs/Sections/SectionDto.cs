namespace Enrollify.Application.DTOs.Sections;

public record SectionDto(
    Guid Id,
    string Name,
    string GradeLevel,
    string SchoolYear,
    int Capacity,
    int CurrentCount,
    string? Adviser,
    bool IsActive);

public record CreateSectionRequest(
    string Name,
    string GradeLevel,
    string SchoolYear,
    int Capacity,
    string? Adviser);

public record UpdateSectionRequest(
    string Name,
    string GradeLevel,
    string SchoolYear,
    int Capacity,
    string? Adviser,
    bool IsActive);
