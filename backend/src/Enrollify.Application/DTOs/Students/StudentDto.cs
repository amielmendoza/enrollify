using Enrollify.Application.DTOs.Learners;

namespace Enrollify.Application.DTOs.Students;

public record StudentDto(
    Guid Id,
    string? LRN,
    string FirstName,
    string MiddleName,
    string LastName,
    DateTime BirthDate,
    string? Gender,
    string Address,
    string? ContactNumber,
    string? Email,
    string? GuardianName,
    string? GuardianContact,
    string FullName,
    Guid? UserId = null,
    Guid? ParentUserId = null,
    bool IsActive = true,
    string StudentNumber = "",
    bool LrnNeedsReview = false,
    LearnerProfileDto? Profile = null);

public record SetStudentActiveRequest(bool IsActive);

public record CreateStudentRequest(
    string? LRN,
    string FirstName,
    string MiddleName,
    string LastName,
    DateTime BirthDate,
    string? Gender,
    string Address,
    string? ContactNumber,
    string? Email,
    string? GuardianName,
    string? GuardianContact,
    LearnerProfileDto? Profile = null);

public record UpdateStudentRequest(
    string? LRN,
    string FirstName,
    string MiddleName,
    string LastName,
    DateTime BirthDate,
    string? Gender,
    string Address,
    string? ContactNumber,
    string? Email,
    string? GuardianName,
    string? GuardianContact,
    LearnerProfileDto? Profile = null);

public record UpdateMyProfileRequest(
    string? ContactNumber,
    string? Email,
    string? Address,
    string? GuardianName,
    string? GuardianContact);

public record UpdateChildProfileRequest(
    string? ContactNumber,
    string? Email,
    string? Address,
    string? GuardianName,
    string? GuardianContact);
