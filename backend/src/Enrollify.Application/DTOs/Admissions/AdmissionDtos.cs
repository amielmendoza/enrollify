namespace Enrollify.Application.DTOs.Admissions;

public record SubmitApplicationRequest(
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? ContactNumber,
    string Gender,
    DateTime DateOfBirth,
    string? Address,
    string GradeLevel,
    string SchoolYear,
    string? PreviousSchool,
    string? PreviousSchoolAddress,
    string? GuardianName,
    string? GuardianContact,
    string? GuardianRelationship);

public record ReviewApplicationRequest(
    bool IsApproved,
    string? Notes);

public record ApplicationListDto(
    Guid Id,
    string ApplicationNumber,
    string FullName,
    string Email,
    string GradeLevel,
    string SchoolYear,
    string Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt);

public record ApplicationDetailDto(
    Guid Id,
    string ApplicationNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? ContactNumber,
    string Gender,
    DateTime DateOfBirth,
    string? Address,
    string GradeLevel,
    string SchoolYear,
    string? PreviousSchool,
    string? PreviousSchoolAddress,
    string? GuardianName,
    string? GuardianContact,
    string? GuardianRelationship,
    string Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes,
    Guid? StudentId);
