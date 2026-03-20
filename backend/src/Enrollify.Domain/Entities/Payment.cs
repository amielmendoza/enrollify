using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class Payment : TenantEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = default!;

    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = default!;
    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
}
