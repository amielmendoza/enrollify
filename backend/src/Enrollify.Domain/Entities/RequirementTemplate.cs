using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class RequirementTemplate : TenantEntity
{
    public string DocumentName { get; set; } = default!;
    public string? GradeLevel { get; set; } // null = applies to all grade levels
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    /// <summary>Submitted once per learner: a verified copy carries forward to later enrollments.</summary>
    public bool KeepOnFile { get; set; }
}
