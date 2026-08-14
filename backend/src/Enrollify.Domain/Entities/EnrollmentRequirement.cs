using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class EnrollmentRequirement : TenantEntity
{
    public Guid EnrollmentId { get; set; }
    public Enrollment Enrollment { get; set; } = default!;
    public string DocumentName { get; set; } = default!;
    public bool IsSubmitted { get; set; }
    public string? FileName { get; set; }
    public string? Notes { get; set; }
    public bool IsVerified { get; set; }
    public string? VerifiedBy { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? ReviewNotes { get; set; }
}
