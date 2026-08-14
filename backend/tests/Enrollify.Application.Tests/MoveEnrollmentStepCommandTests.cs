using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class MoveEnrollmentStepCommandTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedEnrollmentAsync(
        EnrollmentStatus status, Action<Enrollment>? mutate = null)
    {
        var ctx = TestDb.Create();

        var student = new Student { LRN = "LRN-0001", FirstName = "Juan", LastName = "Dela Cruz", Address = "Manila" };
        ctx.Students.Add(student);

        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = status
        };
        mutate?.Invoke(enrollment);
        ctx.Enrollments.Add(enrollment);

        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    private static Fee Fee(string name, decimal amount, bool isActive = true, string schoolYear = "2025-2026", string gradeLevel = "Grade 7") => new()
    {
        Name = name,
        Amount = amount,
        IsActive = isActive,
        SchoolYear = schoolYear,
        GradeLevel = gradeLevel
    };

    // ----- Submitted → Assessed -----

    [Fact]
    public async Task Assess_WithNoActiveFees_Throws()
    {
        var (ctx, enrollment) = await SeedEnrollmentAsync(EnrollmentStatus.Submitted);

        var handler = new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new MoveEnrollmentStepCommand(enrollment.Id, null), default));

        Assert.Contains("No active fees", ex.Message);
        Assert.Equal(EnrollmentStatus.Submitted, enrollment.Status);
    }

    [Fact]
    public async Task Assess_SnapshotsMatchingActiveFees_AndWritesAssessedTotal()
    {
        var (ctx, enrollment) = await SeedEnrollmentAsync(EnrollmentStatus.Submitted);
        ctx.Fees.AddRange(
            Fee("Tuition", 8000m),
            Fee("Miscellaneous", 2000m),
            Fee("Old Tuition", 500m, isActive: false),          // inactive → excluded
            Fee("Other Grade", 999m, gradeLevel: "Grade 8"));   // different grade → excluded
        await ctx.SaveChangesAsync();

        var handler = new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender());
        var dto = await handler.Handle(new MoveEnrollmentStepCommand(enrollment.Id, "Assessed by registrar"), default);

        Assert.Equal(EnrollmentStatus.Assessed, dto.Status);
        Assert.Equal(10000m, enrollment.AssessedTotal);
        Assert.NotNull(enrollment.AssessedAt);

        var snapshot = await ctx.EnrollmentFees.Where(f => f.EnrollmentId == enrollment.Id).ToListAsync();
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(10000m, snapshot.Sum(f => f.Amount));

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Equal(EnrollmentStatus.Submitted, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Assessed, history.ToStatus);
    }

    // ----- Approved → Paid -----

    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedApprovedAsync(
        string? plan, decimal assessedTotal, decimal approvedPaid, PaymentTerm? term = null)
    {
        var (ctx, enrollment) = await SeedEnrollmentAsync(EnrollmentStatus.Approved, e =>
        {
            e.PaymentPlan = plan;
            e.AssessedTotal = assessedTotal;
            e.AssessedAt = DateTime.UtcNow;
        });

        if (term != null)
            ctx.PaymentTerms.Add(term);

        if (approvedPaid > 0)
            ctx.Payments.Add(new Payment { EnrollmentId = enrollment.Id, Amount = approvedPaid, PaymentMethod = "Cash", Status = "Approved" });

        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    private static PaymentTerm MonthlyTerm(decimal downPercent) => new()
    {
        SchoolYear = "2025-2026",
        PlanType = "Monthly",
        DownPaymentPercent = downPercent,
        InterestRatePercent = 5m,
        InstallmentCount = 9,
        IsActive = true
    };

    [Fact]
    public async Task Paid_TermDrivenDownPayment_BlocksUnderpayment()
    {
        // Term demands 40% down of 10000 = 4000; only 3999.99 approved.
        var (ctx, enrollment) = await SeedApprovedAsync("Monthly", 10000m, 3999.99m, MonthlyTerm(40m));

        var handler = new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new MoveEnrollmentStepCommand(enrollment.Id, null), default));

        Assert.StartsWith("Cannot mark as paid", ex.Message);
        Assert.Equal(EnrollmentStatus.Approved, enrollment.Status);
    }

    [Fact]
    public async Task Paid_TermDrivenDownPayment_PassesAtThreshold()
    {
        var (ctx, enrollment) = await SeedApprovedAsync("Monthly", 10000m, 4000m, MonthlyTerm(40m));

        var handler = new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender());
        var dto = await handler.Handle(new MoveEnrollmentStepCommand(enrollment.Id, null), default);

        Assert.Equal(EnrollmentStatus.Paid, dto.Status);
    }

    [Fact]
    public async Task Paid_Monthly_NullTerm_FallsBackTo20PercentDown()
    {
        // No PaymentTerm configured → 20% of 10000 = 2000 required.
        var (blockedCtx, blocked) = await SeedApprovedAsync("Monthly", 10000m, 1999.99m);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new MoveEnrollmentStepCommandHandler(blockedCtx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(blocked.Id, null), default));

        var (passCtx, passing) = await SeedApprovedAsync("Monthly", 10000m, 2000m);
        var dto = await new MoveEnrollmentStepCommandHandler(passCtx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(passing.Id, null), default);
        Assert.Equal(EnrollmentStatus.Paid, dto.Status);
    }

    [Fact]
    public async Task Paid_Quarterly_NullTerm_FallsBackTo30PercentDown()
    {
        // No PaymentTerm configured → 30% of 10000 = 3000 required.
        var (blockedCtx, blocked) = await SeedApprovedAsync("Quarterly", 10000m, 2999.99m);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => new MoveEnrollmentStepCommandHandler(blockedCtx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(blocked.Id, null), default));

        var (passCtx, passing) = await SeedApprovedAsync("Quarterly", 10000m, 3000m);
        var dto = await new MoveEnrollmentStepCommandHandler(passCtx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(passing.Id, null), default);
        Assert.Equal(EnrollmentStatus.Paid, dto.Status);
    }

    [Fact]
    public async Task Paid_FullPlan_OwesOnlyDiscountedTotal()
    {
        var fullTerm = new PaymentTerm
        {
            SchoolYear = "2025-2026",
            PlanType = "Full",
            DiscountPercent = 5m,
            InstallmentCount = 1,
            IsActive = true
        };

        // 10000 - 5% discount = 9500 owed; exactly 9500 approved passes.
        var (ctx, enrollment) = await SeedApprovedAsync("Full", 10000m, 9500m, fullTerm);

        var handler = new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender());
        var dto = await handler.Handle(new MoveEnrollmentStepCommand(enrollment.Id, null), default);

        Assert.Equal(EnrollmentStatus.Paid, dto.Status);
    }

    // ----- Paid → Enrolled -----

    [Fact]
    public async Task Enroll_WithoutSection_Throws()
    {
        var (ctx, enrollment) = await SeedEnrollmentAsync(EnrollmentStatus.Paid);

        var handler = new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new MoveEnrollmentStepCommand(enrollment.Id, null), default));

        Assert.Contains("assign a section", ex.Message);
    }

    [Fact]
    public async Task Enroll_WithSection_Succeeds()
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0002", FirstName = "Maria", LastName = "Santos", Address = "Quezon City" };
        var section = new Section { Name = "Sampaguita", GradeLevel = "Grade 7", SchoolYear = "2025-2026", Capacity = 40 };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Paid,
            SectionId = section.Id
        };
        ctx.Students.Add(student);
        ctx.Sections.Add(section);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();

        var handler = new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender());
        var dto = await handler.Handle(new MoveEnrollmentStepCommand(enrollment.Id, null), default);

        Assert.Equal(EnrollmentStatus.Enrolled, dto.Status);
        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Equal(EnrollmentStatus.Paid, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Enrolled, history.ToStatus);
    }

    [Fact]
    public async Task Enroll_SendsFinalizedEmail()
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0006", FirstName = "Nina", LastName = "Lopez", Address = "Cebu", Email = "nina@example.com" };
        var section = new Section { Name = "Rosal", GradeLevel = "Grade 7", SchoolYear = "2025-2026", Capacity = 40 };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = EnrollmentStatus.Paid,
            SectionId = section.Id
        };
        ctx.Students.Add(student);
        ctx.Sections.Add(section);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();

        var email = new RecordingEmailSender();
        var dto = await new MoveEnrollmentStepCommandHandler(ctx, email)
            .Handle(new MoveEnrollmentStepCommand(enrollment.Id, null), default);

        Assert.Equal(EnrollmentStatus.Enrolled, dto.Status);
        var sent = Assert.Single(email.Sent);
        Assert.Equal("nina@example.com", sent.To);
        Assert.Contains("Enrollment finalized", sent.Subject);
        Assert.Contains("Grade 7", sent.Body);
        Assert.Contains("2025-2026", sent.Body);
        Assert.Contains("Rosal", sent.Body);
    }
}
