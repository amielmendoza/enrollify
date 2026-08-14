using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class RequirementTemplate : TenantEntity
{
    public string DocumentName { get; set; } = default!;
    public string? GradeLevel { get; set; } // null = applies to all grade levels
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
