using Enrollify.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.SchoolProfile;

/// <summary>
/// Placeholder DepEd school profile so the SF1 / LIS reports have something to print on a fresh or
/// demo tenant. Applied only when the tenant's DepEd profile is completely empty — once a school
/// has entered any DepEd value (or cleared the placeholder partially) it is left alone, so the
/// seeder never overwrites real data. Returns 1 if the tenant was touched, else 0. Caller saves.
/// </summary>
public static class DefaultDepEdProfile
{
    public static async Task<int> EnsureForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant == null) return 0;

        var isEmpty = string.IsNullOrWhiteSpace(tenant.DepEdSchoolId)
                      && string.IsNullOrWhiteSpace(tenant.DepEdRegionCode)
                      && string.IsNullOrWhiteSpace(tenant.DepEdRegionName)
                      && string.IsNullOrWhiteSpace(tenant.DepEdDivision)
                      && string.IsNullOrWhiteSpace(tenant.DepEdDistrict)
                      && string.IsNullOrWhiteSpace(tenant.SchoolHeadName);
        if (!isEmpty) return 0;

        tenant.DepEdSchoolId = "000000";
        tenant.DepEdRegionCode = "1300000000";
        tenant.DepEdRegionName = "National Capital Region (NCR)";
        tenant.DepEdDivision = "Demo Division";
        tenant.DepEdDistrict = "Demo District";
        tenant.SchoolHeadName = "Demo School Head";
        return 1;
    }
}
