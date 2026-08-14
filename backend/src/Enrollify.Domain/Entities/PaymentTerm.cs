using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

public class PaymentTerm : TenantEntity
{
    public string SchoolYear { get; set; } = default!;
    public string PlanType { get; set; } = default!; // Full, Monthly, Quarterly
    public decimal DownPaymentPercent { get; set; }
    public decimal InterestRatePercent { get; set; }
    public decimal DiscountPercent { get; set; }
    public int InstallmentCount { get; set; }
    public bool IsActive { get; set; } = true;
}
