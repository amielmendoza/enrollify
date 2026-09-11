using Enrollify.Domain.Entities;

namespace Enrollify.Application.Features.SchoolYears;

/// <summary>
/// The standard Full/Monthly/Quarterly payment-term triple every school year needs.
/// Single source of truth for tenant provisioning (CreateTenantCommand) and new-year
/// provisioning (CreateSchoolYearCommand); mirrors the boot-time seeder's defaults.
/// </summary>
public static class DefaultPaymentTerms
{
    public static readonly (string PlanType, decimal DownPaymentPercent, decimal InterestRatePercent, decimal DiscountPercent, int InstallmentCount)[] Standard =
    {
        ("Full", 0m, 0m, 5m, 1),
        ("Monthly", 20m, 5m, 0m, 9),
        ("Quarterly", 30m, 3m, 0m, 3),
    };

    /// <summary>
    /// Builds the standard three PaymentTerm rows for a school year. Pass
    /// <paramref name="tenantId"/> when creating them outside a tenant-scoped request
    /// (e.g. SuperAdmin provisioning); otherwise SaveChanges assigns the current tenant.
    /// </summary>
    public static List<PaymentTerm> Build(string schoolYear, Guid? tenantId = null) =>
        Standard.Select(p => new PaymentTerm
        {
            TenantId = tenantId ?? Guid.Empty,
            SchoolYear = schoolYear,
            PlanType = p.PlanType,
            DownPaymentPercent = p.DownPaymentPercent,
            InterestRatePercent = p.InterestRatePercent,
            DiscountPercent = p.DiscountPercent,
            InstallmentCount = p.InstallmentCount,
            IsActive = true
        }).ToList();
}
