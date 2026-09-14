using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class RequirementRejectionRecoveryTests
{
    private static async Task<(ApplicationDbContext Ctx, EnrollmentRequirement Requirement, Enrollment Enrollment)> SeedAsync(
        EnrollmentStatus status)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-1501", FirstName = "Rex", LastName = "Docs", Address = "Makati" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 9",
            Status = status,
            AssessedTotal = status >= EnrollmentStatus.Assessed && status != EnrollmentStatus.Cancelled ? 10000m : null
        };
        var requirement = new EnrollmentRequirement
        {
            EnrollmentId = enrollment.Id,
            DocumentName = "Form 138",
            IsSubmitted = true,
            FileName = "form138.pdf"
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        ctx.EnrollmentRequirements.Add(requirement);
        await ctx.SaveChangesAsync();
        return (ctx, requirement, enrollment);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Assessed)]
    [InlineData(EnrollmentStatus.Approved)]
    public async Task Reject_AtAssessedOrApproved_ReturnsEnrollmentToDraft(EnrollmentStatus status)
    {
        var (ctx, requirement, enrollment) = await SeedAsync(status);

        await new ReviewRequirementCommandHandler(ctx)
            .Handle(new ReviewRequirementCommand(requirement.Id, IsVerified: false, "Wrong school year on the card", "Registrar Rita"), default);

        Assert.False(requirement.IsSubmitted); // must be re-uploaded
        Assert.Equal(EnrollmentStatus.Draft, enrollment.Status);
        Assert.Equal(10000m, enrollment.AssessedTotal); // snapshot survives; replaced on re-assessment

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Equal(status, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Draft, history.ToStatus);
        Assert.Contains("returned to Draft for re-upload", history.Remarks);
        Assert.Contains("Form 138", history.Remarks);
    }

    [Fact]
    public async Task Reject_AtPaid_MarksRejected_ButLeavesStatusAlone()
    {
        var (ctx, requirement, enrollment) = await SeedAsync(EnrollmentStatus.Paid);

        await new ReviewRequirementCommandHandler(ctx)
            .Handle(new ReviewRequirementCommand(requirement.Id, IsVerified: false, "Illegible", "Registrar Rita"), default);

        Assert.False(requirement.IsVerified);
        Assert.False(requirement.IsSubmitted);
        Assert.Equal(EnrollmentStatus.Paid, enrollment.Status); // money-locked: never walked back
        Assert.Empty(await ctx.EnrollmentStatusHistories.Where(h => h.EnrollmentId == enrollment.Id).ToListAsync());
    }
}
