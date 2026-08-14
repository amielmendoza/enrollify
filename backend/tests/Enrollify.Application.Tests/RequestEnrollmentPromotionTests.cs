using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// Grade resolution for re-enrollment: (1) promote from the most recent non-cancelled
/// enrollment in a different school year, (2) approved application's grade, (3) "Grade 7".
/// </summary>
public class RequestEnrollmentPromotionTests
{
    private const string ActiveYear = "2026-2027";

    private static async Task<(ApplicationDbContext Ctx, Student Student, Guid UserId)> SeedStudentAsync()
    {
        var ctx = TestDb.Create();
        var userId = Guid.NewGuid();
        var student = new Student
        {
            LRN = "LRN-0100",
            FirstName = "Ben",
            LastName = "Torres",
            Address = "Taguig",
            UserId = userId
        };
        ctx.Students.Add(student);
        ctx.SchoolYears.Add(new SchoolYear
        {
            Name = ActiveYear,
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2027, 3, 31),
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        return (ctx, student, userId);
    }

    private static Enrollment PastEnrollment(Guid studentId, string schoolYear, string gradeLevel, EnrollmentStatus status = EnrollmentStatus.Enrolled) => new()
    {
        StudentId = studentId,
        SchoolYear = schoolYear,
        GradeLevel = gradeLevel,
        Status = status
    };

    private static AdmissionApplication ApprovedApplication(Guid studentId, string gradeLevel) => new()
    {
        ApplicationNumber = "APP-0001",
        FirstName = "Ben",
        LastName = "Torres",
        Email = "ben@example.com",
        Gender = "Male",
        GradeLevel = gradeLevel,
        SchoolYear = ActiveYear,
        Status = "Approved",
        StudentId = studentId
    };

    [Fact]
    public async Task Student_ReEnrollment_PromotesFromPreviousYearEnrollment()
    {
        var (ctx, student, userId) = await SeedStudentAsync();
        ctx.Enrollments.Add(PastEnrollment(student.Id, "2025-2026", "Grade 7"));
        await ctx.SaveChangesAsync();

        var dto = await new StudentRequestEnrollmentCommandHandler(ctx)
            .Handle(new StudentRequestEnrollmentCommand(userId), default);

        Assert.Equal("Grade 8", dto.GradeLevel);
        Assert.Equal(ActiveYear, dto.SchoolYear);
    }

    [Fact]
    public async Task Student_ReEnrollment_UsesMostRecentPreviousYear()
    {
        var (ctx, student, userId) = await SeedStudentAsync();
        ctx.Enrollments.Add(PastEnrollment(student.Id, "2023-2024", "Grade 5"));
        ctx.Enrollments.Add(PastEnrollment(student.Id, "2024-2025", "Grade 6"));
        await ctx.SaveChangesAsync();

        var dto = await new StudentRequestEnrollmentCommandHandler(ctx)
            .Handle(new StudentRequestEnrollmentCommand(userId), default);

        Assert.Equal("Grade 7", dto.GradeLevel);
    }

    [Fact]
    public async Task Student_ReEnrollment_IgnoresCancelledEnrollments()
    {
        var (ctx, student, userId) = await SeedStudentAsync();
        ctx.Enrollments.Add(PastEnrollment(student.Id, "2025-2026", "Grade 7", EnrollmentStatus.Cancelled));
        ctx.AdmissionApplications.Add(ApprovedApplication(student.Id, "Grade 9"));
        await ctx.SaveChangesAsync();

        var dto = await new StudentRequestEnrollmentCommandHandler(ctx)
            .Handle(new StudentRequestEnrollmentCommand(userId), default);

        Assert.Equal("Grade 9", dto.GradeLevel);
    }

    [Fact]
    public async Task Student_FirstEnrollment_UsesApprovedApplicationGrade()
    {
        var (ctx, student, userId) = await SeedStudentAsync();
        ctx.AdmissionApplications.Add(ApprovedApplication(student.Id, "Grade 10"));
        await ctx.SaveChangesAsync();

        var dto = await new StudentRequestEnrollmentCommandHandler(ctx)
            .Handle(new StudentRequestEnrollmentCommand(userId), default);

        Assert.Equal("Grade 10", dto.GradeLevel);
    }

    [Fact]
    public async Task Student_FirstEnrollment_NoHistory_DefaultsToGrade7()
    {
        var (ctx, _, userId) = await SeedStudentAsync();

        var dto = await new StudentRequestEnrollmentCommandHandler(ctx)
            .Handle(new StudentRequestEnrollmentCommand(userId), default);

        Assert.Equal("Grade 7", dto.GradeLevel);
    }

    [Fact]
    public async Task Student_ReEnrollment_Grade12StaysGrade12()
    {
        var (ctx, student, userId) = await SeedStudentAsync();
        ctx.Enrollments.Add(PastEnrollment(student.Id, "2025-2026", "Grade 12"));
        await ctx.SaveChangesAsync();

        var dto = await new StudentRequestEnrollmentCommandHandler(ctx)
            .Handle(new StudentRequestEnrollmentCommand(userId), default);

        Assert.Equal("Grade 12", dto.GradeLevel);
    }

    [Fact]
    public async Task Parent_ReEnrollment_PromotesFromPreviousYearEnrollment()
    {
        var ctx = TestDb.Create();
        var parentUserId = Guid.NewGuid();
        var student = new Student
        {
            LRN = "LRN-0200",
            FirstName = "Cara",
            LastName = "Torres",
            Address = "Taguig",
            ParentUserId = parentUserId
        };
        ctx.Students.Add(student);
        ctx.SchoolYears.Add(new SchoolYear
        {
            Name = ActiveYear,
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2027, 3, 31),
            IsActive = true
        });
        ctx.Enrollments.Add(PastEnrollment(student.Id, "2025-2026", "Grade 3"));
        await ctx.SaveChangesAsync();

        var dto = await new RequestEnrollmentCommandHandler(ctx)
            .Handle(new RequestEnrollmentCommand(student.Id, parentUserId), default);

        Assert.Equal("Grade 4", dto.GradeLevel);
        Assert.Equal(ActiveYear, dto.SchoolYear);
    }
}
