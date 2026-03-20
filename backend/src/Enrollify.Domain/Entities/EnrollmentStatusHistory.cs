using Enrollify.Domain.Common;
using Enrollify.Domain.Enums;

namespace Enrollify.Domain.Entities;

public class EnrollmentStatusHistory : TenantEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = default!;

    public EnrollmentStatus FromStatus { get; set; }
    public EnrollmentStatus ToStatus { get; set; }
    public string? Remarks { get; set; }
    public DateTime TransitionDate { get; set; } = DateTime.UtcNow;
}
