using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

/// <summary>
/// A manual debit/credit entry posted by a registrar against an enrollment's ledger
/// (fee waiver, penalty, correction). Audit-safe: adjustments are add-only and voidable —
/// never edited or hard-deleted.
/// </summary>
public class LedgerAdjustment : TenantEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = default!;

    /// <summary>"Debit" (increases amount owed) or "Credit" (reduces amount owed).</summary>
    public string Type { get; set; } = default!;
    public string Description { get; set; } = default!;

    /// <summary>Always positive; direction comes from <see cref="Type"/>.</summary>
    public decimal Amount { get; set; }

    public string PostedBy { get; set; } = default!;

    public bool IsVoided { get; set; }
    public string? VoidedBy { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
}
