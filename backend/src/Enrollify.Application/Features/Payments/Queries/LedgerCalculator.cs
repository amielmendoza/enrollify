using Enrollify.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

public record LedgerEntryDto(
    DateTime Date,
    string Type,            // Charge | Discount | Interest | Adjustment | Payment
    string Description,
    string? Reference,
    decimal? Debit,
    decimal? Credit,
    decimal Balance,
    Guid? AdjustmentId,
    bool Voided);

public record LedgerDto(
    List<LedgerEntryDto> Entries,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal Balance)
{
    public static LedgerDto Empty() => new(new List<LedgerEntryDto>(), 0m, 0m, 0m);
}

/// <summary>
/// Builds the statement of account (ledger) for one enrollment: assessed fee charges,
/// plan discount/interest, manual adjustments, and approved payments in chronological
/// order with a running balance. The ledger opens at assessment — before AssessedTotal
/// is set it is empty. Discount/interest mirror PaymentsCalculator.EffectiveTotal exactly
/// so the ledger's bottom line and the balance endpoints never disagree.
/// </summary>
public static class LedgerCalculator
{
    public static async Task<LedgerDto> BuildAsync(IApplicationDbContext context, Guid enrollmentId, CancellationToken ct)
    {
        var enrollment = await context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct);

        if (enrollment == null || enrollment.AssessedTotal == null)
            return LedgerDto.Empty();

        var assessedAt = enrollment.AssessedAt ?? enrollment.CreatedAt;
        var totalFees = enrollment.AssessedTotal.Value;

        // Pending entries carry a same-date tiebreak (Order): charges first, then
        // discount/interest, then everything else. LINQ OrderBy is stable, so rows with
        // equal date+order keep their insertion order.
        var pending = new List<(DateTime Date, int Order, string Type, string Description, string? Reference,
            decimal? Debit, decimal? Credit, Guid? AdjustmentId, bool Voided)>();

        // a. Charges — one per snapshot row captured at assessment.
        var snapshotFees = await context.EnrollmentFees
            .Where(f => f.EnrollmentId == enrollment.Id)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);

        foreach (var fee in snapshotFees)
        {
            var description = string.IsNullOrWhiteSpace(fee.Description) ? fee.Name : $"{fee.Name} — {fee.Description}";
            pending.Add((assessedAt, 0, "Charge", description, null, fee.Amount, null, null, false));
        }

        // b. Discount / interest — mirrors PaymentsCalculator.EffectiveTotal. A null or
        //    unknown plan adds nothing: the ledger shows the raw assessed charges only,
        //    consistent with EffectiveTotal's default branch.
        var plan = enrollment.PaymentPlan;
        var term = plan != null
            ? await context.PaymentTerms.FirstOrDefaultAsync(
                t => t.SchoolYear == enrollment.SchoolYear && t.PlanType == plan && t.IsActive, ct)
            : null;

        switch (plan)
        {
            case "Full":
                var discPct = term?.DiscountPercent ?? 0m;
                var discount = Math.Round(totalFees * discPct / 100, 2);
                if (discount > 0)
                    pending.Add((assessedAt, 1, "Discount", $"Full payment discount ({discPct:0.##}%)", null, null, discount, null, false));
                break;

            case "Monthly":
            case "Quarterly":
                var downPct = term?.DownPaymentPercent ?? (plan == "Monthly" ? 20m : 30m);
                var intPct = term?.InterestRatePercent ?? 0m;
                var downPayment = Math.Round(totalFees * downPct / 100, 2);
                var interest = Math.Round((totalFees - downPayment) * intPct / 100, 2);
                if (interest > 0)
                    pending.Add((assessedAt, 1, "Interest", $"Installment interest ({intPct:0.##}%)", null, interest, null, null, false));
                break;
        }

        // c. Manual adjustments — voided rows are shown (marked) but excluded from the
        //    running balance and totals.
        var adjustments = await context.LedgerAdjustments
            .Where(a => a.EnrollmentId == enrollment.Id)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        foreach (var adj in adjustments)
        {
            pending.Add((adj.CreatedAt, 2, "Adjustment", adj.Description, null,
                adj.Type == "Debit" ? adj.Amount : null,
                adj.Type == "Credit" ? adj.Amount : null,
                adj.Id, adj.IsVoided));
        }

        // d. Approved payments.
        var payments = await context.Payments
            .Where(p => p.EnrollmentId == enrollment.Id && p.Status == "Approved")
            .OrderBy(p => p.PaymentDate)
            .ToListAsync(ct);

        foreach (var payment in payments)
            pending.Add((payment.PaymentDate, 2, "Payment", $"Payment — {payment.PaymentMethod}", payment.ReferenceNumber, null, payment.Amount, null, false));

        var entries = new List<LedgerEntryDto>();
        decimal running = 0m, totalDebits = 0m, totalCredits = 0m;

        foreach (var e in pending.OrderBy(x => x.Date).ThenBy(x => x.Order))
        {
            if (!e.Voided)
            {
                totalDebits += e.Debit ?? 0m;
                totalCredits += e.Credit ?? 0m;
                running += (e.Debit ?? 0m) - (e.Credit ?? 0m);
            }

            entries.Add(new LedgerEntryDto(e.Date, e.Type, e.Description, e.Reference, e.Debit, e.Credit, running, e.AdjustmentId, e.Voided));
        }

        return new LedgerDto(entries, totalDebits, totalCredits, totalDebits - totalCredits);
    }
}
