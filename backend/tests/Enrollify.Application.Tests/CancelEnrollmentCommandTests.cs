using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class CancelEnrollmentCommandTests
{
    private static async Task<(ApplicationDbContext Ctx, Enrollment Enrollment)> SeedAsync(EnrollmentStatus status)
    {
        var ctx = TestDb.Create();
        var student = new Student { LRN = "LRN-0003", FirstName = "Pedro", LastName = "Reyes", Address = "Pasig" };
        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = "2025-2026",
            GradeLevel = "Grade 8",
            Status = status
        };
        ctx.Students.Add(student);
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();
        return (ctx, enrollment);
    }

    [Fact]
    public async Task Cancel_SetsStatusAndWritesHistory()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Submitted);

        var handler = new CancelEnrollmentCommandHandler(ctx);
        var dto = await handler.Handle(new CancelEnrollmentCommand(enrollment.Id, "Family relocated", "Admin Ana"), default);

        Assert.Equal(EnrollmentStatus.Cancelled, dto.Status);
        Assert.Equal("Family relocated", dto.Remarks);

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Equal(EnrollmentStatus.Submitted, history.FromStatus);
        Assert.Equal(EnrollmentStatus.Cancelled, history.ToStatus);
        Assert.Contains("Cancelled by Admin Ana", history.Remarks);
        Assert.Contains("Family relocated", history.Remarks);
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_Throws()
    {
        var (ctx, enrollment) = await SeedAsync(EnrollmentStatus.Cancelled);

        var handler = new CancelEnrollmentCommandHandler(ctx);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new CancelEnrollmentCommand(enrollment.Id, "again", "Admin Ana"), default));

        Assert.Contains("already cancelled", ex.Message);
        Assert.Empty(await ctx.EnrollmentStatusHistories.Where(h => h.EnrollmentId == enrollment.Id).ToListAsync());
    }
}
