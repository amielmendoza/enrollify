namespace Enrollify.Application.DTOs.Admissions;

public record SubmitApplicationRequest(
    // "Parent" or "Student" — chosen on the /apply form when caller is anonymous.
    // Ignored when caller is an authenticated parent (always treated as Parent).
    string ApplicationType,
    // Parent fields — required when ApplicationType="Parent" and caller is anonymous; otherwise ignored.
    string? ParentFirstName,
    string? ParentLastName,
    string? ParentEmail,
    string? ParentContactNumber,
    // One or more applicants. For "Parent" mode, this is the list of children being enrolled (1+).
    // For "Student" mode, this must contain exactly one entry (the student themselves).
    List<ApplicantData> Applicants);

public record ApplicantData(
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
    // Map keyed by ApplicationFormField.FieldKey for any custom fields the admin has defined.
    Dictionary<string, string?>? CustomFieldValues);

public record ReviewApplicationRequest(
    bool IsApproved,
    string? Notes);

// Public status lookup (anonymous, by application number) — deliberately minimal:
// applicant first name only, no contact details, no addresses.
public record ApplicationStatusDto(
    string ApplicationNumber,
    string ApplicantName,
    string GradeLevel,
    string SchoolYear,
    string Status,
    DateTime SubmittedAt,
    DateTime? ReviewedAt,
    string? ReviewNotes);

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
    Guid? StudentId,
    string ApplicationType,
    string? ParentFirstName,
    string? ParentLastName,
    string? ParentEmail,
    string? ParentContactNumber,
    Dictionary<string, string?>? CustomFieldValues);
