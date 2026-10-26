using Enrollify.Domain.Common;
using Enrollify.Domain.ValueObjects;

namespace Enrollify.Domain.Entities;

public class Student : TenantEntity, ILearnerProfile
{
    /// <summary>Enrollify-issued "YYYY-NNNNN" (StudentNumbering). Never confused with the LRN.</summary>
    public string StudentNumber { get; set; } = string.Empty;
    /// <summary>DepEd-issued Learner Reference Number (12 digits). Null until known.</summary>
    public string? LRN { get; set; }
    /// <summary>Set by the AddLearnerRecord migration on every pre-existing LRN; cleared by Confirm LRN or an LRN edit.</summary>
    public bool LrnNeedsReview { get; set; }
    public string FirstName { get; set; } = default!;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = default!;
    public DateTime BirthDate { get; set; }
    public string? Gender { get; set; }
    /// <summary>House no. / street / sitio line. The structured part lives in HomeAddress.</summary>
    public string Address { get; set; } = default!;
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianContact { get; set; }
    /// <summary>Guardian's relationship to the learner (e.g. Mother); copied from the approved application. Printed on the SF1.</summary>
    public string? GuardianRelationship { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? UserId { get; set; }          // Set when the student self-registered (Student-role User)
    public Guid? ParentUserId { get; set; }    // Set when a parent registered the student (Parent-role User)

    // DepEd BEEF learner fields (ILearnerProfile)
    public string? ExtensionName { get; set; }
    public string? PsaBirthCertificateNumber { get; set; }
    public string? PlaceOfBirth { get; set; }
    public string? MotherTongue { get; set; }
    public string? Religion { get; set; }
    public bool IsIndigenousPeople { get; set; }
    public string? IpCommunity { get; set; }
    public bool Is4PsBeneficiary { get; set; }
    public string? FourPsHouseholdId { get; set; }
    public bool HasDisability { get; set; }
    public string? DisabilityTypes { get; set; }
    public string? FatherLastName { get; set; }
    public string? FatherFirstName { get; set; }
    public string? FatherMiddleName { get; set; }
    public string? FatherContactNumber { get; set; }
    public string? MotherMaidenLastName { get; set; }
    public string? MotherMaidenFirstName { get; set; }
    public string? MotherMaidenMiddleName { get; set; }
    public string? MotherContactNumber { get; set; }
    public PsgcAddress HomeAddress { get; set; } = new();

    public string FullName => $"{LastName}, {FirstName} {MiddleName}".TrimEnd();

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
