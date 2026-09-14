using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Payments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class PaymentCreationGuardTests
{
    private static async Task<(ApplicationDbContext Ctx, Student Student, Enrollment Enrollment)> SeedAsync(
        EnrollmentStatus status, bool assessed = false)
    {
        var ctx = TestDb.Create();
        var userId = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var student = new Student
        {
            LRN = "LRN-1301", FirstName = "Gia", LastName = "Guard", Address = "Davao",
            UserId = userId, ParentUserId = parentId
        };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = status,
            AssessedTotal = assessed ? 10000m : null,
            AssessedAt = assessed ? DateTime.UtcNow : null
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();
        return (ctx, student, enrollment);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Draft)]
    [InlineData(EnrollmentStatus.Submitted)]
    public async Task Staff_BeforeAssessment_Rejected(EnrollmentStatus status)
    {
        var (ctx, _, enrollment) = await SeedAsync(status);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new CreatePaymentCommandHandler(ctx)
            .Handle(new CreatePaymentCommand(enrollment.Id, 500m, "Cash", null, null), default));

        Assert.Contains("Record the assessment first", ex.Message);
        Assert.Empty(await ctx.Payments.ToListAsync());
    }

    [Fact]
    public async Task Staff_OnCancelled_Rejected()
    {
        var (ctx, _, enrollment) = await SeedAsync(EnrollmentStatus.Cancelled, assessed: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new CreatePaymentCommandHandler(ctx)
            .Handle(new CreatePaymentCommand(enrollment.Id, 500m, "Cash", null, null), default));

        Assert.Contains("cancelled", ex.Message);
        Assert.Empty(await ctx.Payments.ToListAsync());
    }

    [Fact]
    public async Task SelfService_WithOnlyCancelledEnrollment_CannotPay()
    {
        // EnrollmentSelector never yields a cancelled enrollment, so both self-service paths
        // fail fast with "No enrollment found." (the in-handler Cancelled guard is defense in
        // depth behind that). Either way: no money can land on a cancelled enrollment.
        var (ctx, student, _) = await SeedAsync(EnrollmentStatus.Cancelled, assessed: true);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ParentPaymentCommandHandler(ctx)
            .Handle(new ParentPaymentCommand(student.Id, student.ParentUserId!.Value, 500m, "GCash", null, null), default));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => new StudentPaymentCommandHandler(ctx)
            .Handle(new StudentPaymentCommand(student.UserId!.Value, 500m, "GCash", null, null), default));

        Assert.Empty(await ctx.Payments.ToListAsync());
    }

    [Fact]
    public async Task Adjustment_OnCancelled_Rejected()
    {
        // Assessed then cancelled: the Cancelled guard must fire before the assessment check.
        var (ctx, _, enrollment) = await SeedAsync(EnrollmentStatus.Cancelled, assessed: true);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new PostLedgerAdjustmentCommandHandler(ctx)
            .Handle(new PostLedgerAdjustmentCommand(enrollment.Id, "Credit", "Waiver", 100m, "Registrar Rita"), default));

        Assert.Equal("This enrollment is cancelled; its books are closed.", ex.Message);
        Assert.Empty(await ctx.LedgerAdjustments.ToListAsync());
    }
}
