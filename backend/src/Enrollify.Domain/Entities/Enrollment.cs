using Enrollify.Domain.Common;
using Enrollify.Domain.Enums;

namespace Enrollify.Domain.Entities;

public class Enrollment : TenantEntity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = default!;

    public string SchoolYear { get; set; } = default!;
    public string GradeLevel { get; set; } = default!;

    public Guid? SectionId { get; set; }
    public Section? Section { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Draft;
    public string? Remarks { get; set; }
    public string? PaymentPlan { get; set; } // Full, Monthly, Quarterly

    public ICollection<EnrollmentStatusHistory> StatusHistory { get; set; } = new List<EnrollmentStatusHistory>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<EnrollmentRequirement> Requirements { get; set; } = new List<EnrollmentRequirement>();
}
