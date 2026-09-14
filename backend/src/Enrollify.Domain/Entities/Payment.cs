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
    public string? ReceiptFileName { get; set; }
    public string? ReceiptFileUrl { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Voided
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }

    // Void audit trail (set when Status becomes "Voided"; a voided payment is kept
    // for the record, never deleted).
    public string? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
}
