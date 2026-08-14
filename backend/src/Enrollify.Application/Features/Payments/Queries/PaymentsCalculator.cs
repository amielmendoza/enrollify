using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using Enrollify.Application.Features.Enrollments;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

/// <summary>
/// Shared payment-schedule calculation used by both the parent-context and student-self payment views.
/// Builds the balance, fee breakdown, payment list, and installment schedule for a single student's
/// most-recent enrollment.
/// </summary>
public record PaymentsView(
    BalanceDto Balance,
    List<PaymentDto> Payments,
    string? PaymentPlan,
    List<FeeLineDto> Fees,
    List<InstallmentDto> Schedule,
    decimal? DiscountAmount,
    decimal? InterestAmount);

public static class PaymentsCalculator
{
    public static async Task<PaymentsView> BuildAsync(IApplicationDbContext context, Guid studentId, CancellationToken ct)
    {
        var enrollment = await EnrollmentSelector.PickCurrentAsync(context,
            context.Enrollments.Include(e => e.Payments).Where(e => e.StudentId == studentId), ct);

        if (enrollment == null)
            return new PaymentsView(new BalanceDto(0, 0, 0), new List<PaymentDto>(), null, new(), new(), null, null);

        // Prefer the snapshot captured at assessment time so later fee-catalog edits
        // don't retroactively change this enrollment's balance. Enrollments assessed
        // before snapshots existed (AssessedTotal == null) keep the live-catalog math.
        decimal totalFees;
        List<FeeLineDto> feeLines;
        if (enrollment.AssessedTotal.HasValue)
        {
            var snapshotFees = await context.EnrollmentFees
                .Where(f => f.EnrollmentId == enrollment.Id)
                .ToListAsync(ct);

            totalFees = enrollment.AssessedTotal.Value;
            feeLines = snapshotFees
                .Select(f => new FeeLineDto(f.Name, f.Description, f.Amount))
                .ToList();
        }
        else
        {
            var feeEntities = await context.Fees
                .Where(f => f.SchoolYear == enrollment.SchoolYear && f.GradeLevel == enrollment.GradeLevel && f.IsActive)
                .ToListAsync(ct);

            totalFees = feeEntities.Sum(f => f.Amount);
            feeLines = feeEntities
                .Select(f => new FeeLineDto(f.Name, f.Description, f.Amount))
                .ToList();
        }

        var totalPaid = enrollment.Payments.Where(p => p.Status == "Approved").Sum(p => p.Amount);

        var payments = enrollment.Payments
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentDto(p.Id, p.EnrollmentId, p.Amount,
                p.PaymentMethod, p.ReferenceNumber, p.Remarks, p.PaymentDate,
                p.Status, p.ReviewedBy, p.ReviewedAt, p.ReviewNotes,
                p.ReceiptFileName, p.ReceiptFileUrl))
            .ToList();

        var plan = enrollment.PaymentPlan;
        var term = plan != null
            ? await context.PaymentTerms
                .FirstOrDefaultAsync(t => t.SchoolYear == enrollment.SchoolYear && t.PlanType == plan && t.IsActive, ct)
            : null;

        var schoolYear = await context.SchoolYears
            .FirstOrDefaultAsync(sy => sy.Name == enrollment.SchoolYear, ct);
        var syStart = schoolYear?.StartDate ?? enrollment.CreatedAt;

        var schedule = new List<InstallmentDto>();
        decimal? discountAmount = null;
        decimal? interestAmount = null;
        var effectiveTotal = totalFees;

        if (plan == "Full")
        {
            var discPct = term?.DiscountPercent ?? 0;
            var discount = Math.Round(totalFees * discPct / 100, 2);
            effectiveTotal = totalFees - discount;
            if (discount > 0) discountAmount = discount;
            schedule.Add(new InstallmentDto(1, "Full Payment", effectiveTotal, syStart, totalPaid >= effectiveTotal));
        }
        else if (plan == "Monthly" || plan == "Quarterly")
        {
            var downPct = term?.DownPaymentPercent ?? (plan == "Monthly" ? 20m : 30m);
            var intPct = term?.InterestRatePercent ?? 0;
            var count = term?.InstallmentCount ?? (plan == "Monthly" ? 9 : 3);

            var downPayment = Math.Round(totalFees * downPct / 100, 2);
            var remaining = totalFees - downPayment;
            var interest = Math.Round(remaining * intPct / 100, 2);
            var totalRemaining = remaining + interest;
            effectiveTotal = downPayment + totalRemaining;
            if (interest > 0) interestAmount = interest;

            var installmentAmt = Math.Round(totalRemaining / count, 2);
            var lastInstallment = totalRemaining - (installmentAmt * (count - 1));

            schedule.Add(new InstallmentDto(1, $"Down Payment ({downPct:0.##}%)", downPayment, syStart, totalPaid >= downPayment));

            var cumulative = downPayment;
            var monthsPerInstallment = plan == "Quarterly" ? 3 : 1;

            for (int i = 0; i < count; i++)
            {
                var amt = i == count - 1 ? lastInstallment : installmentAmt;
                cumulative += amt;
                var dueDate = syStart.AddMonths((i + 1) * monthsPerInstallment);
                schedule.Add(new InstallmentDto(i + 2,
                    plan == "Quarterly" ? $"{GetOrdinal(i + 2)} Quarter" : $"Month {i + 1}",
                    amt, dueDate, totalPaid >= cumulative));
            }
        }

        // Manual ledger adjustments (waivers, penalties, corrections) fold into the outstanding
        // balance; voided rows are excluded. BalanceDto's TotalFees stays the assessed/catalog
        // figure — adjustments change what is owed, not what was assessed.
        var adjustmentNet = await context.LedgerAdjustments
            .Where(a => a.EnrollmentId == enrollment.Id && !a.IsVoided)
            .SumAsync(a => a.Type == "Debit" ? a.Amount : -a.Amount, ct);

        var balance = new BalanceDto(totalFees, totalPaid, effectiveTotal + adjustmentNet - totalPaid);

        return new PaymentsView(balance, payments, plan, feeLines, schedule, discountAmount, interestAmount);
    }

    /// <summary>
    /// The amount actually owed under a payment plan: Full pays the discounted total,
    /// installment plans pay the total plus interest on the financed remainder.
    /// Keep in sync with the schedule math in BuildAsync — GetBalanceQuery and the
    /// Approved→Paid workflow gate rely on this producing the same figures.
    /// </summary>
    public static decimal EffectiveTotal(string? plan, decimal totalFees, PaymentTerm? term)
    {
        switch (plan)
        {
            case "Full":
                return totalFees - Math.Round(totalFees * (term?.DiscountPercent ?? 0) / 100, 2);
            case "Monthly":
            case "Quarterly":
                var downPct = term?.DownPaymentPercent ?? (plan == "Monthly" ? 20m : 30m);
                var intPct = term?.InterestRatePercent ?? 0;
                var downPayment = Math.Round(totalFees * downPct / 100, 2);
                var interest = Math.Round((totalFees - downPayment) * intPct / 100, 2);
                return totalFees + interest;
            default:
                return totalFees;
        }
    }

    private static string GetOrdinal(int n) => n switch
    {
        1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th"
    };
}
