using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

/// <summary>
/// A fee line snapshotted from the Fee catalog at the moment an enrollment is assessed,
/// so later catalog edits do not retroactively change existing enrollments' balances.
/// </summary>
public class EnrollmentFee : TenantEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
}
