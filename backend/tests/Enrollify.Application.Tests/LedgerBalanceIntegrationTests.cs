using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// Manual ledger adjustments fold into the outstanding balance everywhere balances are
/// reported — but deliberately NOT into the Approved→Paid down-payment gate.
/// </summary>
public class LedgerBalanceIntegrationTests
{
    private static async Task<(ApplicationDbContext Ctx, Student Student, Enrollment Enrollment)> SeedAsync(
        EnrollmentStatus status, string? plan, decimal approvedPaid)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0500", FirstName = "Ely", LastName = "Ramos", Address = "Baguio" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 9",
            Status = status,
            PaymentPlan = plan,
            AssessedTotal = 10000m,
            AssessedAt = DateTime.UtcNow
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        if (approvedPaid > 0)
            ctx.Payments.Add(new Payment { EnrollmentId = enrollment.Id, Amount = approvedPaid, PaymentMethod = "Cash", Status = "Approved" });

        // Active debit 500 counts; voided credit 200 must be ignored everywhere.
        ctx.LedgerAdjustments.Add(new LedgerAdjustment
        {
            EnrollmentId = enrollment.Id,
            Type = "Debit",
            Description = "Damaged equipment",
            Amount = 500m,
            PostedBy = "Registrar Rita"
        });
        ctx.LedgerAdjustments.Add(new LedgerAdjustment
        {
            EnrollmentId = enrollment.Id,
            Type = "Credit",
            Description = "Posted in error",
            Amount = 200m,
            PostedBy = "Registrar Rita",
            IsVoided = true,
            VoidedBy = "Admin Ana",
            VoidedAt = DateTime.UtcNow,
            VoidReason = "Duplicate"
        });

        await ctx.SaveChangesAsync();
        return (ctx, student, enrollment);
    }

    [Fact]
    public async Task Adjustments_FoldIntoPaymentsCalculatorBalance()
    {
        var (ctx, student, _) = await SeedAsync(EnrollmentStatus.Approved, plan: null, approvedPaid: 4000m);

        var view = await PaymentsCalculator.BuildAsync(ctx, student.Id, default);

        // 10000 assessed + 500 active debit - 4000 paid = 6500; voided credit ignored;
        // TotalFees stays the assessed figure.
        Assert.Equal(10000m, view.Balance.TotalFees);
        Assert.Equal(4000m, view.Balance.TotalPaid);
        Assert.Equal(6500m, view.Balance.Balance);
    }

    [Fact]
    public async Task Adjustments_FoldIntoGetBalanceQuery()
    {
        var (ctx, _, enrollment) = await SeedAsync(EnrollmentStatus.Approved, plan: null, approvedPaid: 4000m);

        var balance = await new GetBalanceQueryHandler(ctx).Handle(new GetBalanceQuery(enrollment.Id), default);

        Assert.Equal(10000m, balance.TotalFees);
        Assert.Equal(4000m, balance.TotalPaid);
        Assert.Equal(6500m, balance.Balance);
    }

    [Fact]
    public async Task Adjustments_DoNotAffectApprovedToPaidGate()
    {
        // Monthly plan, no term → gate requires 20% of the ASSESSED 10000 = 2000, even though
        // the active debit adjustment raises the balance owed. Exactly 2000 approved passes.
        var (ctx, _, enrollment) = await SeedAsync(EnrollmentStatus.Approved, plan: "Monthly", approvedPaid: 2000m);

        var handler = new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender());
        var dto = await handler.Handle(new MoveEnrollmentStepCommand(enrollment.Id, null), default);

        Assert.Equal(EnrollmentStatus.Paid, dto.Status);
    }
}
