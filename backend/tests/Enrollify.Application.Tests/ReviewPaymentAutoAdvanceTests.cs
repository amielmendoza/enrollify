using Enrollify.Application.Features.Payments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class ReviewPaymentAutoAdvanceTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment, Payment Pending)> SeedAsync(
        EnrollmentStatus status, string? plan, decimal pendingAmount,
        PaymentTerm? term = null, decimal previouslyApproved = 0m)
    {
        var ctx = TestDb.Create();
        var student = new Student
        {
            LRN = "LRN-0700",
            FirstName = "Paolo",
            LastName = "Garcia",
            Address = "Cavite",
            Email = "payer@example.com"
        };
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
        var pending = new Payment
        {
            EnrollmentId = enrollment.Id,
            Amount = pendingAmount,
            PaymentMethod = "GCash",
            Status = "Pending"
        };

        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        ctx.Payments.Add(pending);
        if (term != null)
            ctx.PaymentTerms.Add(term);
        if (previouslyApproved > 0)
            ctx.Payments.Add(new Payment { EnrollmentId = enrollment.Id, Amount = previouslyApproved, PaymentMethod = "Cash", Status = "Approved" });

        await ctx.SaveChangesAsync();
        return (ctx, enrollment, pending);
    }

    private static Task<int> HistoryCountAsync(ApplicationDbContext ctx, Guid enrollmentId) =>
        ctx.EnrollmentStatusHistories.CountAsync(h => h.EnrollmentId == enrollmentId);

    [Fact]
    public async Task Approve_FullPaymentAtDiscountedTotal_AutoAdvancesWithHistory()
    {
        var term = new PaymentTerm { SchoolYear = "2025-2026", PlanType = "Full", DiscountPercent = 5m, InstallmentCount = 1, IsActive = true };
        var (ctx, enrollment, pending) = await SeedAsync(EnrollmentStatus.Approved, "Full", 9500m, term);

        var email = new RecordingEmailSender();
        await new ReviewPaymentCommandHandler(ctx, email)
            .Handle(new ReviewPaymentCommand(pending.Id, IsApproved: true, null, "Registrar Rita"), default);

        Assert.Equal(EnrollmentStatus.Paid, enrollment.Status);

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Equal(EnrollmentStatus.Approved, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Paid, history.ToStatus);
        Assert.Contains("Auto-advanced", history.Remarks);

        var sent = Assert.Single(email.Sent);
        Assert.Contains("marked as Paid", sent.Body);
    }

    [Fact]
    public async Task Approve_PartialBelowDownPayment_DoesNotAdvance()
    {
        // Monthly with no term → 20% of 10000 = 2000 required; 1999.99 falls short.
        var (ctx, enrollment, pending) = await SeedAsync(EnrollmentStatus.Approved, "Monthly", 1999.99m);

        await new ReviewPaymentCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewPaymentCommand(pending.Id, IsApproved: true, null, "Registrar Rita"), default);

        Assert.Equal("Approved", pending.Status);
        Assert.Equal(EnrollmentStatus.Approved, enrollment.Status);
        Assert.Equal(0, await HistoryCountAsync(ctx, enrollment.Id));
    }

    [Fact]
    public async Task Approve_MonthlyDownPayment_TermDriven_Advances()
    {
        var term = new PaymentTerm { SchoolYear = "2025-2026", PlanType = "Monthly", DownPaymentPercent = 40m, InterestRatePercent = 5m, InstallmentCount = 9, IsActive = true };
        var (ctx, enrollment, pending) = await SeedAsync(EnrollmentStatus.Approved, "Monthly", 4000m, term);

        await new ReviewPaymentCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewPaymentCommand(pending.Id, IsApproved: true, null, "Registrar Rita"), default);

        Assert.Equal(EnrollmentStatus.Paid, enrollment.Status);
        Assert.Equal(1, await HistoryCountAsync(ctx, enrollment.Id));
    }

    [Fact]
    public async Task Approve_CumulativeWithPriorApprovedPayments_Advances()
    {
        // 1000 already approved + this 1000 crosses the 2000 Monthly fallback threshold.
        var (ctx, enrollment, pending) = await SeedAsync(EnrollmentStatus.Approved, "Monthly", 1000m, previouslyApproved: 1000m);

        await new ReviewPaymentCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewPaymentCommand(pending.Id, IsApproved: true, null, "Registrar Rita"), default);

        Assert.Equal(EnrollmentStatus.Paid, enrollment.Status);
    }

    [Fact]
    public async Task Reject_NeverAdvances()
    {
        var (ctx, enrollment, pending) = await SeedAsync(EnrollmentStatus.Approved, null, 10000m);

        await new ReviewPaymentCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewPaymentCommand(pending.Id, IsApproved: false, "Bounced", "Registrar Rita"), default);

        Assert.Equal("Rejected", pending.Status);
        Assert.Equal(EnrollmentStatus.Approved, enrollment.Status);
        Assert.Equal(0, await HistoryCountAsync(ctx, enrollment.Id));
    }

    [Fact]
    public async Task Approve_WhenEnrollmentNotAtApproved_NeverTouchesStatus()
    {
        // More than enough money, but the enrollment is only Assessed — no advance.
        var (ctx, enrollment, pending) = await SeedAsync(EnrollmentStatus.Assessed, null, 99999m);

        await new ReviewPaymentCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewPaymentCommand(pending.Id, IsApproved: true, null, "Registrar Rita"), default);

        Assert.Equal("Approved", pending.Status);
        Assert.Equal(EnrollmentStatus.Assessed, enrollment.Status);
        Assert.Equal(0, await HistoryCountAsync(ctx, enrollment.Id));
    }
}
