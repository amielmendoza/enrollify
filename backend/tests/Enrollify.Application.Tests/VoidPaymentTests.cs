using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Payments.Commands;
using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class VoidPaymentTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment, Payment Payment)> SeedAsync(
        EnrollmentStatus status, string paymentStatus = "Approved", decimal amount = 2000m,
        string? plan = null, decimal? extraApproved = null)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-1201", FirstName = "Vio", LastName = "Ledger", Address = "Iloilo" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = status,
            PaymentPlan = plan,
            AssessedTotal = 10000m,
            AssessedAt = DateTime.UtcNow
        };
        var payment = new Payment { EnrollmentId = enrollment.Id, Amount = amount, PaymentMethod = "GCash", Status = paymentStatus, ReferenceNumber = "OR-1" };

        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        ctx.Payments.Add(payment);
        if (extraApproved.HasValue)
            ctx.Payments.Add(new Payment { EnrollmentId = enrollment.Id, Amount = extraApproved.Value, PaymentMethod = "Cash", Status = "Approved" });
        await ctx.SaveChangesAsync();
        return (ctx, enrollment, payment);
    }

    private static VoidPaymentCommandHandler Handler(ApplicationDbContext ctx) => new(ctx);

    [Fact]
    public async Task Void_ApprovedPayment_SetsVoidFields_AndDropsOutOfMoneySums()
    {
        var (ctx, enrollment, payment) = await SeedAsync(EnrollmentStatus.Approved);

        var dto = await Handler(ctx).Handle(new VoidPaymentCommand(payment.Id, "Encoded twice", "Registrar Rita"), default);

        Assert.Equal("Voided", dto.Status);
        Assert.Equal("Registrar Rita", dto.VoidedBy);
        Assert.NotNull(dto.VoidedAt);
        Assert.Equal("Encoded twice", dto.VoidReason);

        // Every money sum filters Status == "Approved": the voided amount is gone.
        var balance = await new GetBalanceQueryHandler(ctx).Handle(new GetBalanceQuery(enrollment.Id), default);
        Assert.Equal(0m, balance.TotalPaid);
        Assert.Equal(10000m, balance.Balance);

        // The compensating waiver credit is a cancelled-account mechanism only — a normal
        // void must post no adjustment.
        Assert.Empty(await ctx.LedgerAdjustments.ToListAsync());
    }

    [Fact]
    public async Task Void_AfterCancellation_KeepsBalanceAtZero()
    {
        // Assess → approve a payment → cancel (waiver posts) → the check bounces → void.
        var (ctx, enrollment, payment) = await SeedAsync(EnrollmentStatus.Assessed, amount: 2000m);
        await new CancelEnrollmentCommandHandler(ctx)
            .Handle(new CancelEnrollmentCommand(enrollment.Id, "Family withdrew", "Registrar Rita"), default);

        // Cancel closed the books at zero: waiver = 10000 assessed − 2000 approved = 8000.
        var waiver = await ctx.LedgerAdjustments.SingleAsync();
        Assert.Equal(8000m, waiver.Amount);

        await Handler(ctx).Handle(new VoidPaymentCommand(payment.Id, "Cheque bounced after withdrawal", "Registrar Rita"), default);

        // The void auto-posts a compensating credit so the cancelled account still owes zero.
        var compensating = await ctx.LedgerAdjustments.SingleAsync(a => a.Id != waiver.Id);
        Assert.Equal("Credit", compensating.Type);
        Assert.Equal(2000m, compensating.Amount);
        // N2 formatting is culture-dependent — assert the invariant parts of the description.
        Assert.StartsWith("Waiver adjusted — payment of ₱", compensating.Description);
        Assert.EndsWith("voided after cancellation", compensating.Description);
        Assert.Equal("Registrar Rita", compensating.PostedBy);
        Assert.False(compensating.IsVoided);

        var balance = await new GetBalanceQueryHandler(ctx).Handle(new GetBalanceQuery(enrollment.Id), default);
        Assert.Equal(0m, balance.Balance);
        Assert.Equal(EnrollmentStatus.Cancelled, enrollment.Status); // status never touched by the void
    }

    [Fact]
    public async Task Void_PendingPayment_Rejected()
    {
        var (ctx, _, payment) = await SeedAsync(EnrollmentStatus.Approved, paymentStatus: "Pending");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(ctx).Handle(new VoidPaymentCommand(payment.Id, "oops", "Registrar Rita"), default));

        Assert.Contains("Reject it via review instead", ex.Message);
        Assert.Equal("Pending", payment.Status);
    }

    [Fact]
    public async Task Void_AlreadyVoided_Rejected()
    {
        var (ctx, _, payment) = await SeedAsync(EnrollmentStatus.Approved);
        await Handler(ctx).Handle(new VoidPaymentCommand(payment.Id, "first", "Registrar Rita"), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Handler(ctx).Handle(new VoidPaymentCommand(payment.Id, "again", "Registrar Rita"), default));

        Assert.Equal("Payment is already voided.", ex.Message);
    }

    [Fact]
    public async Task Void_ThatBreaksGate_RollsPaidBackToApproved_WithHistory()
    {
        // Monthly with no term → gate needs 2000; the only approved payment IS the 2000.
        var (ctx, enrollment, payment) = await SeedAsync(EnrollmentStatus.Paid, plan: "Monthly", amount: 2000m);

        await Handler(ctx).Handle(new VoidPaymentCommand(payment.Id, "Bounced cheque", "Registrar Rita"), default);

        Assert.Equal(EnrollmentStatus.Approved, enrollment.Status);
        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Equal(EnrollmentStatus.Paid, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Approved, history.ToStatus);
        Assert.Contains("Rolled back to Approved", history.Remarks);
        Assert.Contains("Bounced cheque", history.Remarks);
    }

    [Fact]
    public async Task Void_WhenRemainingPaymentsStillSatisfyGate_KeepsPaid()
    {
        // Two approved 2000s; voiding one leaves 2000 — exactly the Monthly fallback threshold.
        var (ctx, enrollment, payment) = await SeedAsync(EnrollmentStatus.Paid, plan: "Monthly", amount: 2000m, extraApproved: 2000m);

        await Handler(ctx).Handle(new VoidPaymentCommand(payment.Id, "Duplicate entry", "Registrar Rita"), default);

        Assert.Equal(EnrollmentStatus.Paid, enrollment.Status);
        Assert.Empty(await ctx.EnrollmentStatusHistories.Where(h => h.EnrollmentId == enrollment.Id).ToListAsync());
    }

    [Fact]
    public async Task Void_NeverRegressesEnrolled()
    {
        var (ctx, enrollment, payment) = await SeedAsync(EnrollmentStatus.Enrolled, amount: 10000m);

        var dto = await Handler(ctx).Handle(new VoidPaymentCommand(payment.Id, "Refunded", "Registrar Rita"), default);

        Assert.Equal("Voided", dto.Status);
        Assert.Equal(EnrollmentStatus.Enrolled, enrollment.Status); // the debt surfaces via the balance
        Assert.Empty(await ctx.EnrollmentStatusHistories.Where(h => h.EnrollmentId == enrollment.Id).ToListAsync());
    }

    [Fact]
    public async Task Ledger_ListsVoidedPayment_MarkedAndExcludedFromTotals()
    {
        var (ctx, enrollment, payment) = await SeedAsync(EnrollmentStatus.Approved, amount: 2000m, extraApproved: 3000m);
        ctx.EnrollmentFees.Add(new EnrollmentFee { EnrollmentId = enrollment.Id, Name = "Tuition", Amount = 10000m });
        await ctx.SaveChangesAsync();
        await Handler(ctx).Handle(new VoidPaymentCommand(payment.Id, "Encoded twice", "Registrar Rita"), default);

        var ledger = await LedgerCalculator.BuildAsync(ctx, enrollment.Id, default);

        Assert.Equal(3, ledger.Entries.Count); // charge + approved payment + voided payment (still listed)
        var voided = Assert.Single(ledger.Entries, e => e.Voided);
        Assert.Equal("Payment", voided.Type);
        Assert.Equal(2000m, voided.Credit);

        // Voided credit excluded from running balance and SOA totals, like voided adjustments.
        Assert.Equal(3000m, ledger.TotalCredits);
        Assert.Equal(7000m, ledger.Balance);
        Assert.Equal(7000m, ledger.Entries[^1].Balance);
    }

    [Fact]
    public void Validator_RequiresReason_Max300()
    {
        var validator = new VoidPaymentCommandValidator();

        Assert.False(validator.Validate(new VoidPaymentCommand(Guid.NewGuid(), "", "r")).IsValid);
        Assert.False(validator.Validate(new VoidPaymentCommand(Guid.NewGuid(), new string('x', 301), "r")).IsValid);
        Assert.True(validator.Validate(new VoidPaymentCommand(Guid.NewGuid(), "Bounced", "r")).IsValid);
    }
}
