using Enrollify.Application.Features.Students.Commands;
using Enrollify.Application.Features.Students.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

public class StudentActivationTests
{
    private const string ActiveYear = "2026-2027";

    private static async Task<(ApplicationDbContext Ctx, Student Student)> SeedAsync(
        EnrollmentStatus? activeYearEnrollmentStatus = null)
    {
        var ctx = TestDb.Create();
        ctx.SchoolYears.Add(new SchoolYear
        {
            Name = ActiveYear, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31), IsActive = true
        });
        var student = new Student { LRN = "LRN-1701", FirstName = "Dea", LastName = "Active", Address = "Cebu" };
        ctx.Students.Add(student);
        if (activeYearEnrollmentStatus.HasValue)
            ctx.Enrollments.Add(new Enrollment
            {
                StudentId = student.Id, SchoolYear = ActiveYear, GradeLevel = "Grade 7",
                Status = activeYearEnrollmentStatus.Value
            });
        await ctx.SaveChangesAsync();
        return (ctx, student);
    }

    [Fact]
    public async Task Deactivate_WithLiveActiveYearEnrollment_Refuses()
    {
        var (ctx, student) = await SeedAsync(EnrollmentStatus.Enrolled);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => new SetStudentActiveCommandHandler(ctx)
            .Handle(new SetStudentActiveCommand(student.Id, IsActive: false), default));

        Assert.Contains("Cancel the enrollment first", ex.Message);
        Assert.True(student.IsActive);
    }

    [Fact]
    public async Task Deactivate_WithOnlyCancelledEnrollment_Succeeds_AndReactivationIsAlwaysAllowed()
    {
        var (ctx, student) = await SeedAsync(EnrollmentStatus.Cancelled);
        var handler = new SetStudentActiveCommandHandler(ctx);

        var deactivated = await handler.Handle(new SetStudentActiveCommand(student.Id, IsActive: false), default);
        Assert.False(deactivated.IsActive);
        Assert.False(student.IsActive);

        var reactivated = await handler.Handle(new SetStudentActiveCommand(student.Id, IsActive: true), default);
        Assert.True(reactivated.IsActive);
    }

    [Fact]
    public async Task StudentsList_HidesInactiveByDefault_IncludeInactiveShowsThem()
    {
        var (ctx, active) = await SeedAsync();
        var userId = Guid.NewGuid();
        ctx.Students.Add(new Student
        {
            LRN = "LRN-1702", FirstName = "Zoe", LastName = "Zzz", Address = "Cebu",
            IsActive = false, UserId = userId
        });
        await ctx.SaveChangesAsync();

        var defaultList = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery(null), default);
        var row = Assert.Single(defaultList.Items);
        Assert.Equal(active.Id, row.Id);
        Assert.True(row.IsActive);

        var fullList = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery(null, IncludeInactive: true), default);
        Assert.Equal(2, fullList.TotalCount);
        var inactiveRow = fullList.Items.Single(s => s.LRN == "LRN-1702");
        Assert.False(inactiveRow.IsActive);
        Assert.Equal(userId, inactiveRow.UserId); // new DTO fields populated
        Assert.Null(inactiveRow.ParentUserId);
    }
}
