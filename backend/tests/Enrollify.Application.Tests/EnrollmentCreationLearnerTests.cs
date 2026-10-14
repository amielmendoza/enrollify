using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class EnrollmentCreationLearnerTests
{
    private const string Prev = "2025-2026";
    private const string Next = "2026-2027";

    private static async Task<(ApplicationDbContext Ctx, Student S, Guid ParentId)> SeedAsync(
        string? priorGrade = null, string? priorTrack = null, string? priorStrand = null)
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = true });
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "ABM", IsActive = true });
        ctx.SchoolYears.Add(new SchoolYear { Name = Next, IsActive = true, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        var parentId = Guid.NewGuid();
        var s = new Student { StudentNumber = "2025-00001", FirstName = "Ana", LastName = "Cruz", Address = "", ParentUserId = parentId, UserId = Guid.NewGuid() };
        ctx.Students.Add(s);
        if (priorGrade != null)
            ctx.Enrollments.Add(new Enrollment
            {
                StudentId = s.Id, SchoolYear = Prev, GradeLevel = priorGrade, Status = EnrollmentStatus.Enrolled,
                Track = priorTrack, Strand = priorStrand
            });
        await ctx.SaveChangesAsync();
        return (ctx, s, parentId);
    }

    private static Task<Enrollment> NewestAsync(ApplicationDbContext ctx) =>
        ctx.Enrollments.SingleAsync(e => e.SchoolYear == Next);

    [Fact]
    public async Task StaffCreate_FirstEnrollment_IsNew_LaterOnes_AreContinuing()
    {
        var (ctx, s, _) = await SeedAsync();
        await new CreateEnrollmentCommandHandler(ctx).Handle(new CreateEnrollmentCommand(s.Id, Next, "Grade 7"), default);
        Assert.Equal("New", (await NewestAsync(ctx)).LearnerType);

        var (ctx2, s2, _) = await SeedAsync(priorGrade: "Grade 7");
        await new CreateEnrollmentCommandHandler(ctx2).Handle(new CreateEnrollmentCommand(s2.Id, Next, "Grade 8"), default);
        Assert.Equal("Continuing", (await NewestAsync(ctx2)).LearnerType);
    }

    [Fact]
    public async Task StaffCreate_ExplicitLearnerTypeAndStrand_AreKept()
    {
        var (ctx, s, _) = await SeedAsync();
        await new CreateEnrollmentCommandHandler(ctx).Handle(
            new CreateEnrollmentCommand(s.Id, Next, "Grade 11", "Transferee", "academic", "abm"), default);

        var e = await NewestAsync(ctx);
        Assert.Equal("Transferee", e.LearnerType);
        Assert.Equal("ABM", e.Strand);
    }

    [Fact]
    public async Task StaffCreate_StrandOnGrade8_IsRefused()
    {
        var (ctx, s, _) = await SeedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CreateEnrollmentCommandHandler(ctx).Handle(new CreateEnrollmentCommand(s.Id, Next, "Grade 8", null, "Academic", "STEM"), default));
    }

    [Fact]
    public void StaffCreateValidator_RejectsUnknownLearnerType()
    {
        var result = new CreateEnrollmentCommandValidator().Validate(new CreateEnrollmentCommand(Guid.NewGuid(), Next, "Grade 7", "Alumni"));
        Assert.Contains(result.Errors, e => e.PropertyName == "LearnerType");
    }

    [Fact]
    public async Task ParentRequest_Grade10To11_UsesTheRequestedStrand()
    {
        var (ctx, s, parentId) = await SeedAsync(priorGrade: "Grade 10");
        await new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(s.Id, parentId, "Academic", "STEM"), default);

        var e = await NewestAsync(ctx);
        Assert.Equal("Grade 11", e.GradeLevel);
        Assert.Equal("STEM", e.Strand);
        Assert.Equal("Continuing", e.LearnerType);
    }

    [Fact]
    public async Task ParentRequest_Grade10To11_WithoutStrand_LeavesItForTheRegistrar()
    {
        var (ctx, s, parentId) = await SeedAsync(priorGrade: "Grade 10");
        await new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(s.Id, parentId), default);
        Assert.Null((await NewestAsync(ctx)).Strand);
    }

    [Fact]
    public async Task ParentRequest_Grade11To12_CarriesTheStrand()
    {
        var (ctx, s, parentId) = await SeedAsync(priorGrade: "Grade 11", priorTrack: "Academic", priorStrand: "STEM");
        await new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(s.Id, parentId), default);

        var e = await NewestAsync(ctx);
        Assert.Equal("Grade 12", e.GradeLevel);
        Assert.Equal("STEM", e.Strand);
    }

    [Fact]
    public async Task StudentRequest_Grade11To12_CarriesTheStrand()
    {
        var (ctx, s, _) = await SeedAsync(priorGrade: "Grade 11", priorTrack: "Academic", priorStrand: "ABM");
        await new StudentRequestEnrollmentCommandHandler(ctx).Handle(new StudentRequestEnrollmentCommand(s.UserId!.Value), default);
        Assert.Equal("ABM", (await NewestAsync(ctx)).Strand);
    }

    [Fact]
    public async Task Bulk_CarriesStrand11To12_CountsNeedsStrand10To11_AndMarksContinuing()
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = true });
        ctx.SchoolYears.Add(new SchoolYear { Name = Next, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        var g11 = new Student { StudentNumber = "2025-00001", FirstName = "Elf", LastName = "A", Address = "" };
        var g10 = new Student { StudentNumber = "2025-00002", FirstName = "Ten", LastName = "B", Address = "" };
        ctx.Students.AddRange(g11, g10);
        ctx.Enrollments.Add(new Enrollment { StudentId = g11.Id, SchoolYear = Prev, GradeLevel = "Grade 11", Status = EnrollmentStatus.Enrolled, Track = "Academic", Strand = "STEM" });
        ctx.Enrollments.Add(new Enrollment { StudentId = g10.Id, SchoolYear = Prev, GradeLevel = "Grade 10", Status = EnrollmentStatus.Enrolled });
        await ctx.SaveChangesAsync();

        var result = await new BulkReenrollCommandHandler(ctx).Handle(new BulkReenrollCommand(Prev, Next), default);

        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.NeedsStrand);
        var twelve = await ctx.Enrollments.SingleAsync(e => e.SchoolYear == Next && e.StudentId == g11.Id);
        Assert.Equal("STEM", twelve.Strand);
        Assert.Equal("Continuing", twelve.LearnerType);
        Assert.Null((await ctx.Enrollments.SingleAsync(e => e.SchoolYear == Next && e.StudentId == g10.Id)).Strand);
    }

    [Fact]
    public async Task ParentRequest_Grade11To12_KeepsCarriedStrand_EvenIfNowInactive()
    {
        var (ctx, s, parentId) = await SeedAsync(priorGrade: "Grade 11", priorTrack: "Academic", priorStrand: "STEM");
        foreach (var x in ctx.ShsStrands.Where(x => x.Strand == "STEM")) x.IsActive = false;
        await ctx.SaveChangesAsync();
        await new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(s.Id, parentId), default);
        Assert.Equal("STEM", (await NewestAsync(ctx)).Strand);
    }

    [Fact]
    public async Task ParentRequest_InactiveRequestedStrand_IsRefused()
    {
        var (ctx, s, parentId) = await SeedAsync(priorGrade: "Grade 10");
        foreach (var x in ctx.ShsStrands.Where(x => x.Strand == "STEM")) x.IsActive = false;
        await ctx.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(s.Id, parentId, "Academic", "STEM"), default));
    }

    [Fact]
    public async Task Bulk_11To12_WithInactivePriorStrand_CarriesItAndDoesNotCountNeedsStrand()
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = false });
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "ABM", IsActive = true });
        ctx.SchoolYears.Add(new SchoolYear { Name = Next, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        var st = new Student { StudentNumber = "2025-00001", FirstName = "Elf", LastName = "A", Address = "" };
        ctx.Students.Add(st);
        ctx.Enrollments.Add(new Enrollment { StudentId = st.Id, SchoolYear = Prev, GradeLevel = "Grade 11", Status = EnrollmentStatus.Enrolled, Track = "Academic", Strand = "STEM" });
        await ctx.SaveChangesAsync();

        var result = await new BulkReenrollCommandHandler(ctx).Handle(new BulkReenrollCommand(Prev, Next), default);

        Assert.Equal(0, result.NeedsStrand);
        Assert.Equal("STEM", (await NewestAsync(ctx)).Strand);
    }

    [Fact]
    public async Task Bulk_10To11_WhenSchoolHasNoActiveStrands_DoesNotCountNeedsStrand()
    {
        var ctx = TestDb.Create();
        ctx.SchoolYears.Add(new SchoolYear { Name = Next, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        var st = new Student { StudentNumber = "2025-00001", FirstName = "Ten", LastName = "B", Address = "" };
        ctx.Students.Add(st);
        ctx.Enrollments.Add(new Enrollment { StudentId = st.Id, SchoolYear = Prev, GradeLevel = "Grade 10", Status = EnrollmentStatus.Enrolled });
        await ctx.SaveChangesAsync();

        var result = await new BulkReenrollCommandHandler(ctx).Handle(new BulkReenrollCommand(Prev, Next), default);

        Assert.Equal(1, result.Created);
        Assert.Equal(0, result.NeedsStrand);
    }
}
