using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class SendBackEnrollmentTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedAsync(
        EnrollmentStatus status, decimal? approvedPaid = null)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-1401", FirstName = "Sef", LastName = "Backer", Address = "Baguio" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = status,
            AssessedTotal = 10000m,
            AssessedAt = DateTime.UtcNow
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        ctx.EnrollmentFees.Add(new EnrollmentFee { EnrollmentId = enrollment.Id, Name = "Tuition", Amount = 10000m });
        if (approvedPaid.HasValue)
            ctx.Payments.Add(new Payment { EnrollmentId = enrollment.Id, Amount = approvedPaid.Value, PaymentMethod = "Cash", Status = "Approved" });
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    [Fact]
    public async Task SendBack_FromAssessed_ReturnsToSubmitted_KeepingSnapshot()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Assessed);

        var dto = await new SendBackEnrollmentCommandHandler(ctx)
            .Handle(new SendBackEnrollmentCommand(enrollment.Id, "Wrong fee set applied", "Registrar Rita"), default);

        Assert.Equal(EnrollmentStatus.Submitted, dto.Status);

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Equal(EnrollmentStatus.Assessed, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Submitted, history.ToStatus);
        Assert.Equal("Sent back for re-assessment: Wrong fee set applied", history.Remarks);
        Assert.Equal("Registrar Rita", history.CreatedBy);

        // Snapshot deliberately intact — the next assessment replaces it wholesale.
        Assert.Equal(10000m, enrollment.AssessedTotal);
        Assert.Equal(1, await ctx.EnrollmentFees.CountAsync(f => f.EnrollmentId == enrollment.Id));
    }

    [Fact]
    public async Task SendBack_FromApproved_ReturnsToSubmitted_ApprovedPaymentsSurvive()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Approved, approvedPaid: 2000m);

        var dto = await new SendBackEnrollmentCommandHandler(ctx)
            .Handle(new SendBackEnrollmentCommand(enrollment.Id, "Grade level corrected", "Admin Ana"), default);

        Assert.Equal(EnrollmentStatus.Submitted, dto.Status);
        var payment = await ctx.Payments.SingleAsync(p => p.EnrollmentId == enrollment.Id);
        Assert.Equal("Approved", payment.Status); // keeps counting toward the recomputed balance
    }

    [Theory]
    [InlineData(EnrollmentStatus.Draft)]
    [InlineData(EnrollmentStatus.Submitted)]
    [InlineData(EnrollmentStatus.Paid)]
    [InlineData(EnrollmentStatus.Enrolled)]
    public async Task SendBack_FromOtherStatuses_Rejected(EnrollmentStatus status)
    {
        var (ctx, enrollment) = await SeedAsync(status);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new SendBackEnrollmentCommandHandler(ctx)
            .Handle(new SendBackEnrollmentCommand(enrollment.Id, "nope", "Admin Ana"), default));

        Assert.Contains("Only assessed or approved enrollments", ex.Message);
        Assert.Equal(status, enrollment.Status);
    }

    [Fact]
    public async Task SendBack_ThenReassess_ReplacesSnapshotEndToEnd()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Assessed);
        // The fee catalog has changed since the first assessment.
        ctx.Fees.Add(new Fee { Name = "Tuition", Amount = 8000m, SchoolYear = "2025-2026", GradeLevel = "Grade 7", IsActive = true });
        ctx.Fees.Add(new Fee { Name = "Miscellaneous", Amount = 1000m, SchoolYear = "2025-2026", GradeLevel = "Grade 7", IsActive = true });
        await ctx.SaveChangesAsync();

        await new SendBackEnrollmentCommandHandler(ctx)
            .Handle(new SendBackEnrollmentCommand(enrollment.Id, "Catalog corrected", "Registrar Rita"), default);
        Assert.Equal(EnrollmentStatus.Submitted, enrollment.Status);

        // Re-walk Submitted→Assessed: the move-step RemoveRanges the old snapshot and re-adds.
        await new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new MoveEnrollmentStepCommand(enrollment.Id, "Re-assessed"), default);

        Assert.Equal(EnrollmentStatus.Assessed, enrollment.Status);
        Assert.Equal(9000m, enrollment.AssessedTotal);

        var snapshot = await ctx.EnrollmentFees.Where(f => f.EnrollmentId == enrollment.Id).ToListAsync();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(9000m, snapshot.Sum(f => f.Amount));
        Assert.DoesNotContain(snapshot, f => f.Amount == 10000m); // old snapshot row is gone
    }
}
