using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class Fee : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string SchoolYear { get; set; } = default!;
    public string GradeLevel { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
