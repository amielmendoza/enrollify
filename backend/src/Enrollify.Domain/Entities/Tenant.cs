using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Subdomain { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;

    // DepEd school profile (printed on the SF1 / LIS reports). Null until the school fills it in.
    public string? DepEdSchoolId { get; set; }       // 6-digit DepEd School ID
    public string? DepEdRegionCode { get; set; }
    public string? DepEdRegionName { get; set; }
    public string? DepEdDivision { get; set; }
    public string? DepEdDistrict { get; set; }
    public string? SchoolHeadName { get; set; }
}
