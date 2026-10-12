using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// Batch C follow-up: adjustments on a cancelled enrollment — the auto-posted waiver
/// credits above all — must not be voidable into an unhealable imbalance.
/// </summary>
public class VoidAdjustmentOnCancelledTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedAssessedAsync()
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-1801", FirstName = "Wil", LastName = "Waiver", Address = "Taguig" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Assessed,
            AssessedTotal = 10000m,
            AssessedAt = DateTime.UtcNow
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    [Fact]
    public async Task CancelWaiverCredit_CannotBeVoided_BalanceStaysZero()
    {
        var (ctx, enrollment) = await SeedAssessedAsync();
        await new CancelEnrollmentCommandHandler(ctx)
            .Handle(new CancelEnrollmentCommand(enrollment.Id, "Family withdrew", "Registrar Rita"), default);
        var waiver = await ctx.LedgerAdjustments.SingleAsync(); // "Cancelled — remaining charges waived", 10000

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new VoidLedgerAdjustmentCommandHandler(ctx)
            .Handle(new VoidLedgerAdjustmentCommand(enrollment.Id, waiver.Id, "undo the waiver", "Admin Ana"), default));

        Assert.Equal("This enrollment is cancelled; its books are closed.", ex.Message);
        Assert.False(waiver.IsVoided);

        var balance = await new GetBalanceQueryHandler(ctx).Handle(new GetBalanceQuery(enrollment.Id), default);
        Assert.Equal(0m, balance.Balance); // the cancelled account still owes exactly zero
    }

    [Fact]
    public async Task AnyAdjustment_OnCancelledEnrollment_CannotBeVoided()
    {
        var (ctx, enrollment) = await SeedAssessedAsync();
        // A discount posted while the enrollment was live...
        var discount = new LedgerAdjustment
        {
            EnrollmentId = enrollment.Id,
            Type = "Credit",
            Description = "Sibling discount",
            Amount = 500m,
            PostedBy = "Registrar Rita"
        };
        ctx.LedgerAdjustments.Add(discount);
        await ctx.SaveChangesAsync();
        // ...then the enrollment is cancelled.
        await new CancelEnrollmentCommandHandler(ctx)
            .Handle(new CancelEnrollmentCommand(enrollment.Id, "Moved away", "Registrar Rita"), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new VoidLedgerAdjustmentCommandHandler(ctx)
            .Handle(new VoidLedgerAdjustmentCommand(enrollment.Id, discount.Id, "posted in error", "Admin Ana"), default));

        Assert.Equal("This enrollment is cancelled; its books are closed.", ex.Message);
        Assert.False(discount.IsVoided);
    }
}
