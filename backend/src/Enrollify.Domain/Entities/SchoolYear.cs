using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class SchoolYear : TenantEntity
{
    public string Name { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}
