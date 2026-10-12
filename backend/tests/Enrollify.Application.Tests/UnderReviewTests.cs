using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class UnderReviewTests
{
    private static async Task<(ApplicationDbContext Ctx, AdmissionApplication App)> SeedAsync(string status = "Submitted")
    {
        var ctx = TestDb.Create();
        var app = new AdmissionApplication
        {
            ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
            FirstName = "Uma",
            LastName = "Review",
            Email = "uma@example.com",
            Gender = "Female",
            DateOfBirth = new DateTime(2012, 4, 4),
            GradeLevel = "Grade 7",
            SchoolYear = "2026-2027",
            Status = status,
            ApplicationType = "Student"
        };
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();
        return (ctx, app);
    }

    [Fact]
    public async Task MarkUnderReview_FromSubmitted_SetsStatusAndActor()
    {
        var (ctx, app) = await SeedAsync();

        var result = await new SetApplicationUnderReviewCommandHandler(ctx)
            .Handle(new SetApplicationUnderReviewCommand(app.Id, "Registrar Rita"), default);

        Assert.True(result);
        Assert.Equal("UnderReview", app.Status);
        Assert.Equal("Registrar Rita", app.ReviewedBy);
    }

    [Fact]
    public async Task MarkUnderReview_AlreadyUnderReview_IsIdempotentNoOp()
    {
        var (ctx, app) = await SeedAsync("UnderReview");
        app.ReviewedBy = "Registrar Rita";
        await ctx.SaveChangesAsync();

        var result = await new SetApplicationUnderReviewCommandHandler(ctx)
            .Handle(new SetApplicationUnderReviewCommand(app.Id, "Admin Ana"), default);

        Assert.True(result); // success, but nothing changes
        Assert.Equal("UnderReview", app.Status);
        Assert.Equal("Registrar Rita", app.ReviewedBy); // original claimer kept
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task MarkUnderReview_FromFinalStatuses_Refused(string status)
    {
        var (ctx, app) = await SeedAsync(status);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new SetApplicationUnderReviewCommandHandler(ctx)
            .Handle(new SetApplicationUnderReviewCommand(app.Id, "Registrar Rita"), default));

        Assert.Contains("Only submitted applications", ex.Message);
        Assert.Equal(status, app.Status);
    }

    [Fact]
    public async Task Review_Approve_FromUnderReview_Works()
    {
        // ReviewApplicationCommand's guard admits Submitted AND UnderReview — the state
        // introduced by SetApplicationUnderReviewCommand never dead-ends.
        var (ctx, app) = await SeedAsync("UnderReview");

        var dto = await new ReviewApplicationCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewApplicationCommand(app.Id, IsApproved: true, "Welcome!"), default);

        Assert.Equal("Approved", dto.Status);
        Assert.NotNull(dto.StudentId);
        Assert.Equal(1, await ctx.Students.CountAsync());
    }

    [Fact]
    public async Task Review_Reject_FromUnderReview_Works()
    {
        var (ctx, app) = await SeedAsync("UnderReview");

        var dto = await new ReviewApplicationCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewApplicationCommand(app.Id, IsApproved: false, "Incomplete documents"), default);

        Assert.Equal("Rejected", dto.Status);
        Assert.Empty(await ctx.Students.ToListAsync());
    }

    // ReviewedBy must name whoever DECIDED, not whoever picked the application up.
    [Fact]
    public async Task Review_RecordsTheDecider_NotWhoeverMarkedItUnderReview()
    {
        var (ctx, app) = await SeedAsync();
        await new SetApplicationUnderReviewCommandHandler(ctx)
            .Handle(new SetApplicationUnderReviewCommand(app.Id, "Registrar Rita"), default);

        await new ReviewApplicationCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewApplicationCommand(app.Id, IsApproved: false, "Incomplete documents", ReviewedBy: "Admin Ana"), default);

        Assert.Equal("Admin Ana", (await ctx.AdmissionApplications.SingleAsync()).ReviewedBy);
    }
}
