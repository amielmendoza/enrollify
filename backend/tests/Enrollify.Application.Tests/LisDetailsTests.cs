using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class LisDetailsTests
{
    private static readonly DateTime Start = new(2025, 6, 2);
    private static readonly DateTime End = new(2026, 3, 31);

    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedAsync(
        EnrollmentStatus status = EnrollmentStatus.Enrolled, DateTime? withdrawalDate = null)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-1601", FirstName = "Lis", LastName = "Detail", Address = "Baguio" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 7",
            Status = status,
            WithdrawalDate = withdrawalDate,
            WithdrawalType = withdrawalDate.HasValue ? "Transferred Out" : null,
            WithdrawalReason = withdrawalDate.HasValue ? "Moved away" : null,
            TransferredToSchool = withdrawalDate.HasValue ? "Other High" : null,
            LisEncodedAt = new DateTime(2025, 9, 1),
            LisEncodedBy = "Registrar Rita"
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        ctx.SchoolYears.Add(new SchoolYear { Name = "2025-2026", StartDate = Start, EndDate = End });
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    [Theory]
    [InlineData(EnrollmentStatus.Draft)]
    [InlineData(EnrollmentStatus.Submitted)]
    [InlineData(EnrollmentStatus.Assessed)]
    [InlineData(EnrollmentStatus.Approved)]
    [InlineData(EnrollmentStatus.Paid)]
    [InlineData(EnrollmentStatus.Enrolled)]
    public async Task Edit_Succeeds_InEveryStatusExceptCancelled(EnrollmentStatus status)
    {
        var (ctx, enrollment) = await SeedAsync(status);

        var dto = await new UpdateEnrollmentLisDetailsCommandHandler(ctx)
            .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, true, new DateTime(2025, 6, 10)), default);

        Assert.True(dto.IsRepeater);
        Assert.Equal(status, dto.Status);
    }

    [Fact]
    public async Task Edit_Cancelled_Throws()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Cancelled);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateEnrollmentLisDetailsCommandHandler(ctx)
                .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, true, null), default));
    }

    [Fact]
    public async Task Edit_UnknownEnrollment_Throws()
    {
        var ctx = TestDb.Create();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            new UpdateEnrollmentLisDetailsCommandHandler(ctx)
                .Handle(new UpdateEnrollmentLisDetailsCommand(Guid.NewGuid(), false, null), default));
    }

    [Fact]
    public async Task FirstAttendance_BeforeSchoolYearStart_Throws()
    {
        var (ctx, enrollment) = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateEnrollmentLisDetailsCommandHandler(ctx)
                .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, false, Start.AddDays(-1)), default));
    }

    [Fact]
    public async Task FirstAttendance_AfterSchoolYearEnd_Throws()
    {
        var (ctx, enrollment) = await SeedAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateEnrollmentLisDetailsCommandHandler(ctx)
                .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, false, End.AddDays(1)), default));
    }

    [Fact]
    public async Task FirstAttendance_OnSchoolYearBoundaries_IsAccepted()
    {
        var (ctx, enrollment) = await SeedAsync();
        var handler = new UpdateEnrollmentLisDetailsCommandHandler(ctx);

        var onStart = await handler.Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, false, Start), default);
        Assert.Equal(Start, onStart.FirstAttendanceDate);

        var onEnd = await handler.Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, false, End), default);
        Assert.Equal(End, onEnd.FirstAttendanceDate);
    }

    [Fact]
    public async Task FirstAttendance_AfterWithdrawalDate_Throws()
    {
        var (ctx, enrollment) = await SeedAsync(withdrawalDate: new DateTime(2025, 10, 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new UpdateEnrollmentLisDetailsCommandHandler(ctx)
                .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, false, new DateTime(2025, 10, 2)), default));
    }

    [Fact]
    public async Task FirstAttendance_OnWithdrawalDate_IsAccepted()
    {
        var withdrawn = new DateTime(2025, 10, 1);
        var (ctx, enrollment) = await SeedAsync(withdrawalDate: withdrawn);

        var dto = await new UpdateEnrollmentLisDetailsCommandHandler(ctx)
            .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, false, withdrawn), default);

        Assert.Equal(withdrawn, dto.FirstAttendanceDate);
    }

    [Fact]
    public async Task FirstAttendance_IgnoresTimeOfDay_OnEndBoundary()
    {
        var (ctx, enrollment) = await SeedAsync();

        var dto = await new UpdateEnrollmentLisDetailsCommandHandler(ctx)
            .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, false, End.AddHours(15)), default);

        Assert.NotNull(dto.FirstAttendanceDate);
    }

    [Fact]
    public async Task Null_FirstAttendance_SkipsDateValidation_AndClearsValue()
    {
        var (ctx, enrollment) = await SeedAsync();
        enrollment.FirstAttendanceDate = new DateTime(2025, 6, 10);
        await ctx.SaveChangesAsync();

        var dto = await new UpdateEnrollmentLisDetailsCommandHandler(ctx)
            .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, true, null), default);

        Assert.Null(dto.FirstAttendanceDate);
        Assert.True(dto.IsRepeater);
    }

    [Fact]
    public async Task ValidDate_Persists()
    {
        var (ctx, enrollment) = await SeedAsync();
        var date = new DateTime(2025, 6, 16);

        await new UpdateEnrollmentLisDetailsCommandHandler(ctx)
            .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, true, date), default);

        var saved = await ctx.Enrollments.AsNoTracking().SingleAsync(e => e.Id == enrollment.Id);
        Assert.True(saved.IsRepeater);
        Assert.Equal(date, saved.FirstAttendanceDate);
    }

    [Fact]
    public async Task ReturnedDto_CarriesAllLisFields()
    {
        var withdrawn = new DateTime(2025, 12, 1);
        var (ctx, enrollment) = await SeedAsync(withdrawalDate: withdrawn);

        var dto = await new UpdateEnrollmentLisDetailsCommandHandler(ctx)
            .Handle(new UpdateEnrollmentLisDetailsCommand(enrollment.Id, true, new DateTime(2025, 6, 9)), default);

        Assert.True(dto.IsRepeater);
        Assert.Equal(new DateTime(2025, 6, 9), dto.FirstAttendanceDate);
        Assert.Equal("Transferred Out", dto.WithdrawalType);
        Assert.Equal(withdrawn, dto.WithdrawalDate);
        Assert.Equal("Moved away", dto.WithdrawalReason);
        Assert.Equal("Other High", dto.TransferredToSchool);
        Assert.Equal(new DateTime(2025, 9, 1), dto.LisEncodedAt);
        Assert.Equal("Registrar Rita", dto.LisEncodedBy);
    }

    // Every EnrollmentDto construction site must map the DepEd/LIS fields — one test per read path.

    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedFullAsync()
    {
        var (ctx, enrollment) = await SeedAsync(withdrawalDate: new DateTime(2025, 12, 1));
        enrollment.IsRepeater = true;
        enrollment.FirstAttendanceDate = new DateTime(2025, 6, 9);
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    private static void AssertLisFields(Enrollify.Application.DTOs.Enrollments.EnrollmentDto dto)
    {
        Assert.True(dto.IsRepeater);
        Assert.Equal(new DateTime(2025, 6, 9), dto.FirstAttendanceDate);
        Assert.Equal("Transferred Out", dto.WithdrawalType);
        Assert.Equal(new DateTime(2025, 12, 1), dto.WithdrawalDate);
        Assert.Equal("Moved away", dto.WithdrawalReason);
        Assert.Equal("Other High", dto.TransferredToSchool);
        Assert.Equal(new DateTime(2025, 9, 1), dto.LisEncodedAt);
        Assert.Equal("Registrar Rita", dto.LisEncodedBy);
    }

    [Fact]
    public async Task GetEnrollmentById_MapsLisFields()
    {
        var (ctx, enrollment) = await SeedFullAsync();
        var dto = await new GetEnrollmentByIdQueryHandler(ctx).Handle(new GetEnrollmentByIdQuery(enrollment.Id), default);
        AssertLisFields(dto);
    }

    [Fact]
    public async Task GetEnrollments_List_MapsLisFields()
    {
        var (ctx, _) = await SeedFullAsync();
        var result = await new GetEnrollmentsQueryHandler(ctx)
            .Handle(new GetEnrollmentsQuery(null, null, null, null, 1, 20, false, null), default);
        AssertLisFields(Assert.Single(result.Items));
    }

    [Fact]
    public async Task SendBack_MapsLisFields()
    {
        var (ctx, enrollment) = await SeedFullAsync();
        enrollment.Status = EnrollmentStatus.Approved;
        await ctx.SaveChangesAsync();

        AssertLisFields(await new SendBackEnrollmentCommandHandler(ctx)
            .Handle(new SendBackEnrollmentCommand(enrollment.Id, "Fix", "Admin"), default));
    }

    [Fact]
    public async Task Cancel_MapsLisFields()
    {
        var (ctx, enrollment) = await SeedFullAsync();

        AssertLisFields(await new CancelEnrollmentCommandHandler(ctx)
            .Handle(new CancelEnrollmentCommand(enrollment.Id, "Withdrew", "Admin"), default));
    }

    [Fact]
    public async Task GetMyEnrollments_MapsLisFields()
    {
        var (ctx, enrollment) = await SeedFullAsync();
        var userId = Guid.NewGuid();
        (await ctx.Students.SingleAsync(s => s.Id == enrollment.StudentId)).UserId = userId;
        await ctx.SaveChangesAsync();

        var list = await new GetMyEnrollmentsQueryHandler(ctx).Handle(new GetMyEnrollmentsQuery(userId), default);
        AssertLisFields(Assert.Single(list));
    }

    [Fact]
    public async Task GetChildEnrollments_MapsLisFields()
    {
        var (ctx, enrollment) = await SeedFullAsync();
        var parentId = Guid.NewGuid();
        (await ctx.Students.SingleAsync(s => s.Id == enrollment.StudentId)).ParentUserId = parentId;
        await ctx.SaveChangesAsync();

        var list = await new GetChildEnrollmentsQueryHandler(ctx)
            .Handle(new GetChildEnrollmentsQuery(enrollment.StudentId, parentId), default);
        AssertLisFields(Assert.Single(list));
    }

    [Fact]
    public async Task AssignSection_MapsLisFields()
    {
        var (ctx, enrollment) = await SeedFullAsync();
        var section = new Section { Name = "Sampaguita", SchoolYear = "2025-2026", GradeLevel = "Grade 7", Capacity = 40 };
        ctx.Sections.Add(section);
        await ctx.SaveChangesAsync();

        AssertLisFields(await new AssignSectionCommandHandler(ctx)
            .Handle(new AssignSectionCommand(enrollment.Id, section.Id), default));
    }

    [Fact]
    public async Task ParentSubmit_MapsLisFields()
    {
        var (ctx, enrollment) = await SeedFullAsync();
        enrollment.Status = EnrollmentStatus.Draft;
        var parentId = Guid.NewGuid();
        (await ctx.Students.SingleAsync(s => s.Id == enrollment.StudentId)).ParentUserId = parentId;
        await ctx.SaveChangesAsync();

        AssertLisFields(await new SubmitEnrollmentCommandHandler(ctx)
            .Handle(new SubmitEnrollmentCommand(enrollment.Id, enrollment.StudentId, parentId), default));
    }

    [Fact]
    public async Task StudentSubmit_MapsLisFields()
    {
        var (ctx, enrollment) = await SeedFullAsync();
        enrollment.Status = EnrollmentStatus.Draft;
        var userId = Guid.NewGuid();
        (await ctx.Students.SingleAsync(s => s.Id == enrollment.StudentId)).UserId = userId;
        await ctx.SaveChangesAsync();

        AssertLisFields(await new StudentSubmitEnrollmentCommandHandler(ctx)
            .Handle(new StudentSubmitEnrollmentCommand(enrollment.Id, userId), default));
    }

    [Fact]
    public async Task MoveStep_MapsLisFields()
    {
        var (ctx, enrollment) = await SeedFullAsync();
        enrollment.Status = EnrollmentStatus.Assessed;
        await ctx.SaveChangesAsync();

        AssertLisFields(await new MoveEnrollmentStepCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new MoveEnrollmentStepCommand(enrollment.Id, null), default));
    }
}
