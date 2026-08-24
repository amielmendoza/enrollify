using Enrollify.Domain.Entities;

namespace Enrollify.Application.Features.Payments;

/// <summary>
/// The Approved→Paid payment gate: the minimum approved-payment total an enrollment must
/// reach before it can be marked Paid. Shared by MoveEnrollmentStepCommand (manual advance)
/// and ReviewPaymentCommand (auto-advance on payment approval) so the two can never disagree.
/// Thresholds mirror PaymentsCalculator: installment plans owe the term's down payment
/// (20%/30% fallbacks for Monthly/Quarterly when no term is configured); Full — and no
/// plan at all — owe the discounted effective total.
/// </summary>
public static class PaymentGate
{
    public static decimal MinimumRequired(string? plan, decimal totalFees, PaymentTerm? term) => plan switch
    {
        "Monthly" => Math.Round(totalFees * (term?.DownPaymentPercent ?? 20m) / 100m, 2),
        "Quarterly" => Math.Round(totalFees * (term?.DownPaymentPercent ?? 30m) / 100m, 2),
        _ => totalFees - Math.Round(totalFees * (term?.DiscountPercent ?? 0m) / 100m, 2)
    };
}
