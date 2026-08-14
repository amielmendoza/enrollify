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

    // Fee snapshot captured when the enrollment is assessed. Null until first assessment;
    // when set, balance reads use these instead of the live Fee catalog.
    public decimal? AssessedTotal { get; set; }
    public DateTime? AssessedAt { get; set; }

    public ICollection<EnrollmentStatusHistory> StatusHistory { get; set; } = new List<EnrollmentStatusHistory>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<EnrollmentRequirement> Requirements { get; set; } = new List<EnrollmentRequirement>();
    public ICollection<EnrollmentFee> FeeSnapshot { get; set; } = new List<EnrollmentFee>();
    public ICollection<LedgerAdjustment> LedgerAdjustments { get; set; } = new List<LedgerAdjustment>();
}
