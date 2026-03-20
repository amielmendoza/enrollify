using Enrollify.Domain.Common;

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

    public int CurrentCount => Enrollments.Count;
    public bool IsFull => CurrentCount >= Capacity;
}
