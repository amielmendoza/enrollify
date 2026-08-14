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

    // Application type: "Parent" (someone enrolling a child) or "Student" (self-enrolling).
    // Determines what kind of User account the registrar's approval will create.
    public string ApplicationType { get; set; } = "Student";

    // Set when an authenticated parent submits an application for an additional child.
    // For anonymous parent applications, this is null at submission time and the parent fields below
    // are used; on approval the registrar resolves (or creates) the Parent User from ParentEmail.
    public Guid? ParentUserId { get; set; }

    // Captured for anonymous "Parent" applications so a Parent User can be created on approval.
    public string? ParentFirstName { get; set; }
    public string? ParentLastName { get; set; }
    public string? ParentEmail { get; set; }
    public string? ParentContactNumber { get; set; }

    // JSON map of custom-field values, keyed by ApplicationFormField.FieldKey.
    // Only populated for fields that the admin defined as custom (IsBuiltIn=false).
    public string? CustomFieldValues { get; set; }
}
