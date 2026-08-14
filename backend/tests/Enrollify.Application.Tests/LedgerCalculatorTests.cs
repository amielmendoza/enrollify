using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

public class LedgerCalculatorTests
{
    private static readonly DateTime AssessDate = new(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);

    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedAssessedAsync(
        string? plan = null, Action<ApplicationDbContext, Enrollment>? extra = null)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0300", FirstName = "Ana", LastName = "Reyes", Address = "Iloilo" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Assessed,
            PaymentPlan = plan,
            AssessedTotal = 10000m,
            AssessedAt = AssessDate
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        ctx.EnrollmentFees.Add(new EnrollmentFee { EnrollmentId = enrollment.Id, Name = "Miscellaneous", Amount = 2000m });
        ctx.EnrollmentFees.Add(new EnrollmentFee { EnrollmentId = enrollment.Id, Name = "Tuition", Description = "Annual tuition", Amount = 8000m });
        extra?.Invoke(ctx, enrollment);
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    [Fact]
    public async Task PreAssessment_ReturnsEmptyLedger()
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0301", FirstName = "Ana", LastName = "Reyes", Address = "Iloilo" };
        var enrollment = new Enrollment { StudentId = student.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Submitted };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();

        var ledger = await LedgerCalculator.BuildAsync(ctx, enrollment.Id, default);

        Assert.Empty(ledger.Entries);
        Assert.Equal(0m, ledger.TotalDebits);
        Assert.Equal(0m, ledger.TotalCredits);
        Assert.Equal(0m, ledger.Balance);
    }

    [Fact]
    public async Task Charges_AppearWithRunningBalance()
    {
        var (ctx, enrollment) = await SeedAssessedAsync();

        var ledger = await LedgerCalculator.BuildAsync(ctx, enrollment.Id, default);

        Assert.Equal(2, ledger.Entries.Count);
        Assert.All(ledger.Entries, e => Assert.Equal("Charge", e.Type));
        Assert.All(ledger.Entries, e => Assert.Equal(AssessDate, e.Date));

        Assert.Equal("Miscellaneous", ledger.Entries[0].Description);
        Assert.Equal(2000m, ledger.Entries[0].Debit);
        Assert.Equal(2000m, ledger.Entries[0].Balance);

        Assert.Equal("Tuition — Annual tuition", ledger.Entries[1].Description);
        Assert.Equal(8000m, ledger.Entries[1].Debit);
        Assert.Equal(10000m, ledger.Entries[1].Balance);

        Assert.Equal(10000m, ledger.TotalDebits);
        Assert.Equal(0m, ledger.TotalCredits);
        Assert.Equal(10000m, ledger.Balance);
    }

    [Fact]
    public async Task FullPlan_DiscountCredit_OrderedAfterCharges()
    {
        var (ctx, enrollment) = await SeedAssessedAsync("Full", (c, _) =>
            c.PaymentTerms.Add(new PaymentTerm
            {
                SchoolYear = "2025-2026",
                PlanType = "Full",
                DiscountPercent = 5m,
                InstallmentCount = 1,
                IsActive = true
            }));

        var ledger = await LedgerCalculator.BuildAsync(ctx, enrollment.Id, default);

        Assert.Equal(3, ledger.Entries.Count);
        var discountEntry = ledger.Entries[2];
        Assert.Equal("Discount", discountEntry.Type);
        Assert.Equal("Full payment discount (5%)", discountEntry.Description);
        Assert.Equal(500m, discountEntry.Credit);
        Assert.Equal(9500m, discountEntry.Balance);

        Assert.Equal(10000m, ledger.TotalDebits);
        Assert.Equal(500m, ledger.TotalCredits);
        Assert.Equal(9500m, ledger.Balance);
    }

    [Fact]
    public async Task MonthlyPlan_InterestDebit_MirrorsEffectiveTotal()
    {
        var term = new PaymentTerm
        {
            SchoolYear = "2025-2026",
            PlanType = "Monthly",
            DownPaymentPercent = 20m,
            InterestRatePercent = 5m,
            InstallmentCount = 9,
            IsActive = true
        };
        var (ctx, enrollment) = await SeedAssessedAsync("Monthly", (c, _) => c.PaymentTerms.Add(term));

        var ledger = await LedgerCalculator.BuildAsync(ctx, enrollment.Id, default);

        // Interest = 5% of (10000 - 2000 down) = 400, same as EffectiveTotal.
        var interestEntry = ledger.Entries[2];
        Assert.Equal("Interest", interestEntry.Type);
        Assert.Equal("Installment interest (5%)", interestEntry.Description);
        Assert.Equal(400m, interestEntry.Debit);
        Assert.Equal(10400m, interestEntry.Balance);

        Assert.Equal(10400m, ledger.TotalDebits);
        Assert.Equal(10400m, ledger.Balance);
        Assert.Equal(PaymentsCalculator.EffectiveTotal("Monthly", 10000m, term), ledger.Balance);
    }

    [Fact]
    public async Task NullPlan_ShowsRawAssessedCharges_NoDiscountOrInterest()
    {
        // Term rows exist, but the enrollment has no plan → neither discount nor interest.
        var (ctx, enrollment) = await SeedAssessedAsync(plan: null, (c, _) =>
            c.PaymentTerms.Add(new PaymentTerm
            {
                SchoolYear = "2025-2026",
                PlanType = "Full",
                DiscountPercent = 5m,
                InstallmentCount = 1,
                IsActive = true
            }));

        var ledger = await LedgerCalculator.BuildAsync(ctx, enrollment.Id, default);

        Assert.All(ledger.Entries, e => Assert.Equal("Charge", e.Type));
        Assert.Equal(10000m, ledger.Balance);
    }

    [Fact]
    public async Task ApprovedPayments_AppearAsCredits_PendingExcluded()
    {
        var (ctx, enrollment) = await SeedAssessedAsync(plan: null, (c, e) =>
        {
            c.Payments.Add(new Payment
            {
                EnrollmentId = e.Id,
                Amount = 2000m,
                PaymentMethod = "GCash",
                ReferenceNumber = "OR-123",
                Status = "Approved",
                PaymentDate = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)
            });
            c.Payments.Add(new Payment
            {
                EnrollmentId = e.Id,
                Amount = 999m,
                PaymentMethod = "Cash",
                Status = "Pending",
                PaymentDate = new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc)
            });
        });

        var ledger = await LedgerCalculator.BuildAsync(ctx, enrollment.Id, default);

        Assert.Equal(3, ledger.Entries.Count); // 2 charges + 1 approved payment
        var paymentEntry = ledger.Entries[2];
        Assert.Equal("Payment", paymentEntry.Type);
        Assert.Equal("Payment — GCash", paymentEntry.Description);
        Assert.Equal("OR-123", paymentEntry.Reference);
        Assert.Equal(2000m, paymentEntry.Credit);
        Assert.Equal(8000m, paymentEntry.Balance);

        Assert.Equal(2000m, ledger.TotalCredits);
        Assert.Equal(8000m, ledger.Balance);
    }

    [Fact]
    public async Task VoidedAdjustment_ShownAndMarked_ButExcludedFromBalanceAndTotals()
    {
        var (ctx, enrollment) = await SeedAssessedAsync(plan: null, (c, e) =>
        {
            c.LedgerAdjustments.Add(new LedgerAdjustment
            {
                EnrollmentId = e.Id,
                Type = "Debit",
                Description = "Lost library book penalty",
                Amount = 300m,
                PostedBy = "Registrar Rita"
            });
            c.LedgerAdjustments.Add(new LedgerAdjustment
            {
                EnrollmentId = e.Id,
                Type = "Credit",
                Description = "Posted in error",
                Amount = 100m,
                PostedBy = "Registrar Rita",
                IsVoided = true,
                VoidedBy = "Admin Ana",
                VoidedAt = DateTime.UtcNow,
                VoidReason = "Duplicate"
            });
        });

        var ledger = await LedgerCalculator.BuildAsync(ctx, enrollment.Id, default);

        Assert.Equal(4, ledger.Entries.Count); // 2 charges + 2 adjustments (voided still listed)

        var voided = Assert.Single(ledger.Entries, e => e.Voided);
        Assert.Equal("Adjustment", voided.Type);
        Assert.Equal(100m, voided.Credit);
        Assert.NotNull(voided.AdjustmentId);

        var active = Assert.Single(ledger.Entries, e => e.Type == "Adjustment" && !e.Voided);
        Assert.Equal(300m, active.Debit);
        Assert.NotNull(active.AdjustmentId);

        // Voided credit excluded everywhere: totals and final balance ignore it.
        Assert.Equal(10300m, ledger.TotalDebits);
        Assert.Equal(0m, ledger.TotalCredits);
        Assert.Equal(10300m, ledger.Balance);
        Assert.Equal(10300m, ledger.Entries[^1].Balance);
    }
}
