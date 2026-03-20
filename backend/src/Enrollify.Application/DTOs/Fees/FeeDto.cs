namespace Enrollify.Application.DTOs.Fees;

public record FeeDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Amount,
    string SchoolYear,
    string GradeLevel,
    bool IsActive);

public record CreateFeeRequest(
    string Name,
    string? Description,
    decimal Amount,
    string SchoolYear,
    string GradeLevel);
