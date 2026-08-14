namespace Enrollify.Application.DTOs.PaymentTerms;

public record PaymentTermDto(
    Guid Id, string SchoolYear, string PlanType,
    decimal DownPaymentPercent, decimal InterestRatePercent,
    decimal DiscountPercent, int InstallmentCount, bool IsActive);

public record CreatePaymentTermRequest(
    string SchoolYear, string PlanType,
    decimal DownPaymentPercent, decimal InterestRatePercent,
    decimal DiscountPercent, int InstallmentCount);

public record UpdatePaymentTermRequest(
    decimal DownPaymentPercent, decimal InterestRatePercent,
    decimal DiscountPercent, int InstallmentCount);
