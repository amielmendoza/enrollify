using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class AdmissionApplication : TenantEntity
{
    public string ApplicationNumber { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? ContactNumber { get; set; }
    public string Gender { get; set; } = default!;
    public DateTime DateOfBirth { get; set; }
    public string? Address { get; set; }
    public string GradeLevel { get; set; } = default!;
    public string SchoolYear { get; set; } = default!;
    public string? PreviousSchool { get; set; }
    public string? PreviousSchoolAddress { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianContact { get; set; }
    public string? GuardianRelationship { get; set; }
    public string Status { get; set; } = "Submitted";
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewNotes { get; set; }
    public Guid? StudentId { get; set; }
}
