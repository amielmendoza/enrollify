using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class Student : TenantEntity
{
    public string LRN { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = default!;
    public DateTime BirthDate { get; set; }
    public string? Gender { get; set; }
    public string Address { get; set; } = default!;
    public string? ContactNumber { get; set; }
    public string? Email { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianContact { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? UserId { get; set; }          // Set when the student self-registered (Student-role User)
    public Guid? ParentUserId { get; set; }    // Set when a parent registered the student (Parent-role User)

    public string FullName => $"{LastName}, {FirstName} {MiddleName}".TrimEnd();

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}
