using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class EnrollmentLearnerInfoTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment E)> SeedAsync(
        string grade = "Grade 11", EnrollmentStatus status = EnrollmentStatus.Draft, bool strandActive = true, bool withFees = true)
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = strandActive });
        var s = new Student { StudentNumber = "2026-00001", LRN = "136512260001", FirstName = "Ana", LastName = "Cruz", Address = "" };
        ctx.Students.Add(s);
        var e = new Enrollment { StudentId = s.Id, SchoolYear = "2026-2027", GradeLevel = grade, Status = status };
        ctx.Enrollments.Add(e);
        if (withFees)
            ctx.Fees.Add(new Fee { Name = "Tuition", Amount = 1000m, SchoolYear = "2026-2027", GradeLevel = grade, IsActive = true });
        await ctx.SaveChangesAsync();
        return (ctx, e);
    }

    private static UpdateEnrollmentLearnerInfoCommand Set(Guid id, string? track = "academic", string? strand = "stem") =>
        new(id, new EnrollmentLearnerInfoDto(LearnerType: "Transferee", LastSchoolId: "305123", Track: track, Strand: strand));

    [Fact]
    public async Task Draft_UpdatesFields_AndNormalizesStrand()
    {
        var (ctx, e) = await SeedAsync();
        var dto = await new UpdateEnrollmentLearnerInfoCommandHandler(ctx).Handle(Set(e.Id), default);

        Assert.Equal("STEM", dto.Strand);
        var reloaded = await ctx.Enrollments.SingleAsync();
        Assert.Equal("Academic", reloaded.Track);
        Assert.Equal("Transferee", reloaded.LearnerType);
        Assert.Equal("305123", reloaded.LastSchoolId);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Assessed)]
    [InlineData(EnrollmentStatus.Enrolled)]
    [InlineData(EnrollmentStatus.Cancelled)]
    public async Task LockedOnceAssessed(EnrollmentStatus status)
    {
        var (ctx, e) = await SeedAsync(status: status);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateEnrollmentLearnerInfoCommandHandler(ctx).Handle(Set(e.Id), default));
        Assert.Contains("locked", ex.Message);
    }

    [Fact]
    public async Task EditableAgain_AfterSendBack()
    {
        var (ctx, e) = await SeedAsync(status: EnrollmentStatus.Submitted); // send-back lands here
        await new UpdateEnrollmentLearnerInfoCommandHandler(ctx).Handle(Set(e.Id), default);
        Assert.Equal("STEM", (await ctx.Enrollments.SingleAsync()).Strand);
    }

    [Fact]
    public async Task StrandOnNonShsGrade_IsRefused()
    {
        var (ctx, e) = await SeedAsync(grade: "Grade 7");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateEnrollmentLearnerInfoCommandHandler(ctx).Handle(Set(e.Id), default));
    }

    [Fact]
    public async Task Assessment_RefusedWithoutStrand_WhenTheSchoolOffersStrands()
    {
        var (ctx, e) = await SeedAsync(status: EnrollmentStatus.Submitted);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(e.Id, null), default));
        Assert.Equal("Set the learner's track/strand before assessment.", ex.Message);
    }

    [Fact]
    public async Task Assessment_AllowedWithoutStrand_WhenEveryStrandIsDeactivated()
    {
        var (ctx, e) = await SeedAsync(status: EnrollmentStatus.Submitted, strandActive: false);
        var dto = await new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(e.Id, null), default);
        Assert.Equal(EnrollmentStatus.Assessed, dto.Status);
    }

    [Fact]
    public async Task Assessment_AllowedWithStrand()
    {
        var (ctx, e) = await SeedAsync(status: EnrollmentStatus.Submitted);
        e.Track = "Academic";
        e.Strand = "STEM";
        await ctx.SaveChangesAsync();

        var dto = await new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender()).Handle(new MoveEnrollmentStepCommand(e.Id, null), default);
        Assert.Equal(EnrollmentStatus.Assessed, dto.Status);
    }

    [Fact]
    public async Task EnrollmentDetail_ExposesStudentNumberLrnAndLearnerInfo()
    {
        var (ctx, e) = await SeedAsync();
        e.LearnerType = "New";
        e.Strand = "STEM";
        await ctx.SaveChangesAsync();

        var dto = await new GetEnrollmentByIdQueryHandler(ctx).Handle(new GetEnrollmentByIdQuery(e.Id), default);

        Assert.Equal("2026-00001", dto.StudentNumber);
        Assert.Equal("136512260001", dto.Lrn);
        Assert.Equal("New", dto.LearnerInfo!.LearnerType);
        Assert.Equal("STEM", dto.LearnerInfo.Strand);
    }
}
