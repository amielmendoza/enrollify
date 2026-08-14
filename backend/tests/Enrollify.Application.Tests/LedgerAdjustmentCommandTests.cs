using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class LedgerAdjustmentCommandTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedAsync(bool assessed)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0400", FirstName = "Ramon", LastName = "Diaz", Address = "Davao" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 8",
            Status = assessed ? EnrollmentStatus.Assessed : EnrollmentStatus.Submitted,
            AssessedTotal = assessed ? 10000m : null,
            AssessedAt = assessed ? DateTime.UtcNow : null
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    // ----- Post -----

    [Fact]
    public async Task Post_BeforeAssessment_Throws()
    {
        var (ctx, enrollment) = await SeedAsync(assessed: false);

        var handler = new PostLedgerAdjustmentCommandHandler(ctx);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new PostLedgerAdjustmentCommand(enrollment.Id, "Debit", "Penalty", 100m, "Registrar Rita"), default));

        Assert.Equal("Adjustments can only be posted after assessment.", ex.Message);
        Assert.Empty(await ctx.LedgerAdjustments.ToListAsync());
    }

    [Fact]
    public async Task Post_InsertsRow_AndReturnsDto()
    {
        var (ctx, enrollment) = await SeedAsync(assessed: true);

        var handler = new PostLedgerAdjustmentCommandHandler(ctx);
        var dto = await handler.Handle(
            new PostLedgerAdjustmentCommand(enrollment.Id, "Credit", "Sibling discount", 250m, "Registrar Rita"), default);

        Assert.Equal("Credit", dto.Type);
        Assert.Equal("Sibling discount", dto.Description);
        Assert.Equal(250m, dto.Amount);
        Assert.Equal("Registrar Rita", dto.PostedBy);

        var row = await ctx.LedgerAdjustments.SingleAsync();
        Assert.Equal(dto.Id, row.Id);
        Assert.Equal(enrollment.Id, row.EnrollmentId);
        Assert.False(row.IsVoided);
        Assert.Null(row.VoidedBy);
    }

    [Fact]
    public void PostValidator_EnforcesTypeDescriptionAndPositiveAmount()
    {
        var validator = new PostLedgerAdjustmentCommandValidator();
        var enrollmentId = Guid.NewGuid();

        Assert.False(validator.Validate(new PostLedgerAdjustmentCommand(enrollmentId, "Refund", "x", 100m, "r")).IsValid);
        Assert.False(validator.Validate(new PostLedgerAdjustmentCommand(enrollmentId, "Debit", "", 100m, "r")).IsValid);
        Assert.False(validator.Validate(new PostLedgerAdjustmentCommand(enrollmentId, "Debit", new string('x', 301), 100m, "r")).IsValid);
        Assert.False(validator.Validate(new PostLedgerAdjustmentCommand(enrollmentId, "Debit", "x", 0m, "r")).IsValid);
        Assert.False(validator.Validate(new PostLedgerAdjustmentCommand(enrollmentId, "Credit", "x", -5m, "r")).IsValid);

        Assert.True(validator.Validate(new PostLedgerAdjustmentCommand(enrollmentId, "Debit", "Penalty", 100m, "r")).IsValid);
        Assert.True(validator.Validate(new PostLedgerAdjustmentCommand(enrollmentId, "Credit", "Waiver", 100m, "r")).IsValid);
    }

    // ----- Void -----

    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment, LedgerAdjustment Adjustment)> SeedWithAdjustmentAsync()
    {
        var (ctx, enrollment) = await SeedAsync(assessed: true);
        var adjustment = new LedgerAdjustment
        {
            EnrollmentId = enrollment.Id,
            Type = "Debit",
            Description = "Penalty",
            Amount = 300m,
            PostedBy = "Registrar Rita"
        };
        ctx.LedgerAdjustments.Add(adjustment);
        await ctx.SaveChangesAsync();
        return (ctx, enrollment, adjustment);
    }

    [Fact]
    public async Task Void_SetsOnlyVoidFields()
    {
        var (ctx, enrollment, adjustment) = await SeedWithAdjustmentAsync();

        var handler = new VoidLedgerAdjustmentCommandHandler(ctx);
        await handler.Handle(new VoidLedgerAdjustmentCommand(enrollment.Id, adjustment.Id, "Posted twice", "Admin Ana"), default);

        Assert.True(adjustment.IsVoided);
        Assert.Equal("Admin Ana", adjustment.VoidedBy);
        Assert.NotNull(adjustment.VoidedAt);
        Assert.Equal("Posted twice", adjustment.VoidReason);

        // Original posting untouched.
        Assert.Equal("Debit", adjustment.Type);
        Assert.Equal(300m, adjustment.Amount);
        Assert.Equal("Registrar Rita", adjustment.PostedBy);
    }

    [Fact]
    public async Task Void_Twice_Throws()
    {
        var (ctx, enrollment, adjustment) = await SeedWithAdjustmentAsync();

        var handler = new VoidLedgerAdjustmentCommandHandler(ctx);
        await handler.Handle(new VoidLedgerAdjustmentCommand(enrollment.Id, adjustment.Id, "Posted twice", "Admin Ana"), default);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new VoidLedgerAdjustmentCommand(enrollment.Id, adjustment.Id, "again", "Admin Ana"), default));
        Assert.Equal("Adjustment is already voided.", ex.Message);
    }

    [Fact]
    public async Task Void_AdjustmentOfDifferentEnrollment_ThrowsNotFound()
    {
        var (ctx, _, adjustment) = await SeedWithAdjustmentAsync();

        var handler = new VoidLedgerAdjustmentCommandHandler(ctx);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new VoidLedgerAdjustmentCommand(Guid.NewGuid(), adjustment.Id, "wrong enrollment", "Admin Ana"), default));
        Assert.False(adjustment.IsVoided);
    }
}
