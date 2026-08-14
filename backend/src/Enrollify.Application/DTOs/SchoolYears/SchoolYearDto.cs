namespace Enrollify.Application.DTOs.SchoolYears;

public record SchoolYearDto(
    Guid Id,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    DateTime CreatedAt);

public record CreateSchoolYearRequest(
    string Name,
    DateTime StartDate,
    DateTime EndDate);
