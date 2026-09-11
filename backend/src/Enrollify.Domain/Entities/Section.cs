using Enrollify.Domain.Common;
using Enrollify.Domain.Enums;

namespace Enrollify.Domain.Entities;

public class Section : TenantEntity
{
    public string Name { get; set; } = default!;
    public string GradeLevel { get; set; } = default!;
    public string SchoolYear { get; set; } = default!;
    public int Capacity { get; set; }
    public string? Adviser { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    /// <summary>
    /// Seats in use. Cancelled enrollments don't hold seats — computed from the loaded
    /// collection, so callers must Include(s => s.Enrollments) for a real figure.
    /// </summary>
    public int CurrentCount => Enrollments.Count(e => e.Status != EnrollmentStatus.Cancelled);
    public bool IsFull => CurrentCount >= Capacity;
}
