using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class CancelEnrollmentCommandTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedAsync(EnrollmentStatus status)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0003", FirstName = "Pedro", LastName = "Reyes", Address = "Pasig" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 8",
            Status = status
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    [Fact]
    public async Task Cancel_SetsStatusAndWritesHistory()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Submitted);

        var handler = new CancelEnrollmentCommandHandler(ctx);
        var dto = await handler.Handle(new CancelEnrollmentCommand(enrollment.Id, "Family relocated", "Admin Ana"), default);

        Assert.Equal(EnrollmentStatus.Cancelled, dto.Status);
        Assert.Equal("Family relocated", dto.Remarks);

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Equal(EnrollmentStatus.Submitted, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Cancelled, history.ToStatus);
        Assert.Contains("Cancelled by Admin Ana", history.Remarks);
        Assert.Contains("Family relocated", history.Remarks);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_Throws()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Cancelled);

        var handler = new CancelEnrollmentCommandHandler(ctx);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new CancelEnrollmentCommand(enrollment.Id, "again", "Admin Ana"), default));

        Assert.Contains("already cancelled", ex.Message);
        Assert.Empty(await ctx.EnrollmentStatusHistories.Where(h => h.EnrollmentId == enrollment.Id).ToListAsync());
    }

    [Fact]
    public async Task Cancel_WithOutstandingBalance_PostsWaiverCredit()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Approved);

        // Assessed 10000 on a Full plan with 5% discount -> effective total 9500.
        enrollment.AssessedTotal = 10000m;
        enrollment.AssessedAt = DateTime.UtcNow;
        enrollment.PaymentPlan = "Full";
        ctx.PaymentTerms.Add(new PaymentTerm
        {
            SchoolYear = "2025-2026", PlanType = "Full",
            DownPaymentPercent = 0, InterestRatePercent = 0, DiscountPercent = 5, InstallmentCount = 1
        });

        // Adjustment net: +200 (the voided credit must be ignored).
        ctx.LedgerAdjustments.AddRange(
            new LedgerAdjustment { EnrollmentId = enrollment.Id, Type = "Debit", Description = "Late penalty", Amount = 200m, PostedBy = "Registrar Maria" },
            new LedgerAdjustment { EnrollmentId = enrollment.Id, Type = "Credit", Description = "Mistaken waiver", Amount = 100m, PostedBy = "Registrar Maria", IsVoided = true, VoidedBy = "Admin Ana", VoidedAt = DateTime.UtcNow });

        // Paid: 4000 approved (the pending one must be ignored).
        ctx.Payments.AddRange(
            new Payment { EnrollmentId = enrollment.Id, Amount = 4000m, PaymentMethod = "Cash", Status = "Approved" },
            new Payment { EnrollmentId = enrollment.Id, Amount = 999m, PaymentMethod = "Cash", Status = "Pending" });
        await ctx.SaveChangesAsync();

        var handler = new CancelEnrollmentCommandHandler(ctx);
        await handler.Handle(new CancelEnrollmentCommand(enrollment.Id, "Family relocated", "Admin Ana"), default);

        // Outstanding = 9500 (effective) + 200 (adjustment net) - 4000 (paid) = 5700.
        var waiver = await ctx.LedgerAdjustments
            .SingleAsync(a => a.EnrollmentId == enrollment.Id && a.Description == "Cancelled — remaining charges waived");
        Assert.Equal("Credit", waiver.Type);
        Assert.Equal(5700m, waiver.Amount);
        Assert.Equal("Admin Ana", waiver.PostedBy);
        Assert.False(waiver.IsVoided);

        // The books are closed: the ledger/balance bottoms out at exactly what was paid.
        var balance = await new GetBalanceQueryHandler(ctx).Handle(new GetBalanceQuery(enrollment.Id), default);
        Assert.Equal(0m, balance.Balance);
    }

    [Fact]
    public async Task Cancel_WithZeroBalance_PostsNoAdjustment()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Paid);

        enrollment.AssessedTotal = 1000m;
        enrollment.AssessedAt = DateTime.UtcNow;
        ctx.Payments.Add(new Payment { EnrollmentId = enrollment.Id, Amount = 1000m, PaymentMethod = "Cash", Status = "Approved" });
        await ctx.SaveChangesAsync();

        var handler = new CancelEnrollmentCommandHandler(ctx);
        await handler.Handle(new CancelEnrollmentCommand(enrollment.Id, "Transferring out", "Admin Ana"), default);

        Assert.Empty(await ctx.LedgerAdjustments.Where(a => a.EnrollmentId == enrollment.Id).ToListAsync());
    }

    [Fact]
    public async Task Cancel_BeforeAssessment_PostsNoAdjustment()
    {
        // AssessedTotal is null: no charges exist yet, so there is nothing to waive.
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Submitted);

        var handler = new CancelEnrollmentCommandHandler(ctx);
        await handler.Handle(new CancelEnrollmentCommand(enrollment.Id, "Changed schools", "Admin Ana"), default);

        Assert.Empty(await ctx.LedgerAdjustments.Where(a => a.EnrollmentId == enrollment.Id).ToListAsync());
    }
}
