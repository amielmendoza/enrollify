namespace Enrollify.Application.DTOs.Students;

public record StudentDto(
    Guid Id,
    string LRN,
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
    string FullName);

public record CreateStudentRequest(
    string LRN,
    string FirstName,
    string MiddleName,
    string LastName,
    DateTime BirthDate,
    string? Gender,
    string Address,
    string? ContactNumber,
    string? Email,
    string? GuardianName,
    string? GuardianContact);

public record UpdateStudentRequest(
    string LRN,
    string FirstName,
    string MiddleName,
    string LastName,
    DateTime BirthDate,
    string? Gender,
    string Address,
    string? ContactNumber,
    string? Email,
    string? GuardianName,
    string? GuardianContact);
