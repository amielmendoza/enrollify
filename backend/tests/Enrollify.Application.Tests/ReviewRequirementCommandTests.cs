using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class ReviewRequirementCommandTests
{
    private static async Task<(ApplicationDbContext Ctx, EnrollmentRequirement Requirement, Enrollment Enrollment)> SeedAsync(
        EnrollmentStatus enrollmentStatus, bool isSubmitted)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0004", FirstName = "Liza", LastName = "Cruz", Address = "Makati" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 9",
            Status = enrollmentStatus
        };
        var requirement = new EnrollmentRequirement
        {
            EnrollmentId = enrollment.Id,
            DocumentName = "PSA Birth Certificate",
            IsSubmitted = isSubmitted,
            FileName = isSubmitted ? "psa.pdf" : null
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        ctx.EnrollmentRequirements.Add(requirement);
        await ctx.SaveChangesAsync();
        return (ctx, requirement, enrollment);
    }

    [Fact]
    public async Task Reject_RollsSubmittedEnrollmentBackToDraft()
    {
        var (ctx, requirement, enrollment) = await SeedAsync(EnrollmentStatus.Submitted, isSubmitted: true);

        var handler = new ReviewRequirementCommandHandler(ctx);
        await handler.Handle(new ReviewRequirementCommand(requirement.Id, IsVerified: false, "Scan is blurry", "Registrar Rita"), default);

        Assert.False(requirement.IsVerified);
        Assert.False(requirement.IsSubmitted); // must be re-uploaded
        Assert.Equal(EnrollmentStatus.Draft, enrollment.Status);

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Equal(EnrollmentStatus.Submitted, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Draft, history.ToStatus);
        Assert.Contains("PSA Birth Certificate", history.Remarks);
    }

    [Fact]
    public async Task Approve_VerifiesRequirement_WithoutChangingEnrollmentStatus()
    {
        var (ctx, requirement, enrollment) = await SeedAsync(EnrollmentStatus.Submitted, isSubmitted: true);

        var handler = new ReviewRequirementCommandHandler(ctx);
        await handler.Handle(new ReviewRequirementCommand(requirement.Id, IsVerified: true, null, "Registrar Rita"), default);

        Assert.True(requirement.IsVerified);
        Assert.True(requirement.IsSubmitted);
        Assert.Equal("Registrar Rita", requirement.VerifiedBy);
        Assert.Equal(EnrollmentStatus.Submitted, enrollment.Status);
        Assert.Empty(await ctx.EnrollmentStatusHistories.Where(h => h.EnrollmentId == enrollment.Id).ToListAsync());
    }

    [Fact]
    public async Task Review_UnsubmittedRequirement_Throws()
    {
        var (ctx, requirement, _) = await SeedAsync(EnrollmentStatus.Draft, isSubmitted: false);

        var handler = new ReviewRequirementCommandHandler(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new ReviewRequirementCommand(requirement.Id, IsVerified: true, null, "Registrar Rita"), default));
    }
}
