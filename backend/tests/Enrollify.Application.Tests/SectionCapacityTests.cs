using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Sections.Commands;
using Enrollify.Application.Features.Sections.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>
/// Section seat accounting: cancelled enrollments must never hold seats — in the entity's
/// computed CurrentCount/IsFull, in the sections listing projection, in the assign-section
/// capacity gate, and via CancelEnrollmentCommand explicitly freeing the seat.
/// </summary>
public class SectionCapacityTests
{
    private static Student NewStudent(string lrn) =>
        new() { LRN = lrn, FirstName = "Test", LastName = "Student", Address = "Manila" };

    /// <summary>Section "Einstein" (Grade 7, 2025-2026) plus one enrollment per given status, all seated in it.</summary>
    private static async Task<(ApplicationDbContext Ctx, Section Section)> SeedSectionAsync(
        int capacity, params EnrollmentStatus[] enrollmentStatuses)
    {
        var ctx = TestDb.Create();
        var section = new Section { Name = "Einstein", GradeLevel = "Grade 7", SchoolYear = "2025-2026", Capacity = capacity };
        ctx.Sections.Add(section);

        for (var i = 0; i < enrollmentStatuses.Length; i++)
        {
            var student = NewStudent($"LRN-SEC-{i:D3}");
            ctx.Students.Add(student);
            ctx.Enrollments.Add(new Enrollment
            {
                StudentId = student.Id,
                SchoolYear = "2025-2026",
                GradeLevel = "Grade 7",
                SectionId = section.Id,
                Status = enrollmentStatuses[i]
            });
        }

        await ctx.SaveChangesAsync();
        return (ctx, section);
    }

    [Fact]
    public void CurrentCount_And_IsFull_ExcludeCancelledEnrollments()
    {
        var section = new Section { Name = "A", GradeLevel = "Grade 7", SchoolYear = "2025-2026", Capacity = 2 };
        section.Enrollments.Add(new Enrollment { SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Enrolled });
        section.Enrollments.Add(new Enrollment { SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Cancelled });

        Assert.Equal(1, section.CurrentCount);
        Assert.False(section.IsFull);

        section.Enrollments.Add(new Enrollment { SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Submitted });

        Assert.Equal(2, section.CurrentCount);
        Assert.True(section.IsFull);
    }

    [Fact]
    public async Task GetSections_CurrentCount_ExcludesCancelled()
    {
        var (ctx, _) = await SeedSectionAsync(40,
            EnrollmentStatus.Enrolled, EnrollmentStatus.Cancelled, EnrollmentStatus.Approved);

        var handler = new GetSectionsQueryHandler(ctx);
        var result = await handler.Handle(new GetSectionsQuery(null, null), default);

        var dto = Assert.Single(result);
        Assert.Equal(2, dto.CurrentCount);
    }

    [Fact]
    public async Task AssignSection_SeatFreedByCancellation_IsReusable()
    {
        // Capacity 1, held only by a cancelled enrollment (legacy shape: SectionId not yet cleared).
        var (ctx, section) = await SeedSectionAsync(1, EnrollmentStatus.Cancelled);

        var student = NewStudent("LRN-SEC-NEW");
        ctx.Students.Add(student);
        var enrollment = new Enrollment { StudentId = student.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Approved };
        ctx.Enrollments.Add(enrollment);
        await ctx.SaveChangesAsync();

        var handler = new AssignSectionCommandHandler(ctx);
        var dto = await handler.Handle(new AssignSectionCommand(enrollment.Id, section.Id), default);

        Assert.Equal(section.Id, dto.SectionId);
    }

    [Fact]
    public async Task AssignSection_GradeOrYearMismatch_Throws()
    {
        var (ctx, section) = await SeedSectionAsync(10); // Grade 7, 2025-2026

        var wrongGradeStudent = NewStudent("LRN-SEC-WG");
        var wrongYearStudent = NewStudent("LRN-SEC-WY");
        ctx.Students.AddRange(wrongGradeStudent, wrongYearStudent);
        var wrongGrade = new Enrollment { StudentId = wrongGradeStudent.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 8", Status = EnrollmentStatus.Approved };
        var wrongYear = new Enrollment { StudentId = wrongYearStudent.Id, SchoolYear = "2024-2025", GradeLevel = "Grade 7", Status = EnrollmentStatus.Approved };
        ctx.Enrollments.AddRange(wrongGrade, wrongYear);
        await ctx.SaveChangesAsync();

        var handler = new AssignSectionCommandHandler(ctx);

        var exGrade = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new AssignSectionCommand(wrongGrade.Id, section.Id), default));
        Assert.Contains("cannot be assigned", exGrade.Message);
        Assert.Contains("Grade 8", exGrade.Message);

        var exYear = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new AssignSectionCommand(wrongYear.Id, section.Id), default));
        Assert.Contains("cannot be assigned", exYear.Message);
        Assert.Contains("2024-2025", exYear.Message);
    }

    [Fact]
    public async Task Cancel_FreesSectionSeat_AndKeepsSectionNameInHistory()
    {
        var (ctx, _) = await SeedSectionAsync(5, EnrollmentStatus.Enrolled);
        var enrollment = await ctx.Enrollments.SingleAsync();

        var handler = new CancelEnrollmentCommandHandler(ctx);
        var dto = await handler.Handle(new CancelEnrollmentCommand(enrollment.Id, "Family relocated", "Admin Ana"), default);

        Assert.Null(dto.SectionId);
        Assert.Null(dto.SectionName);

        var saved = await ctx.Enrollments.AsNoTracking().SingleAsync(e => e.Id == enrollment.Id);
        Assert.Null(saved.SectionId);

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == enrollment.Id);
        Assert.Contains("Einstein", history.Remarks); // section name preserved for the record
    }

    [Fact]
    public async Task DeleteSection_WithSeatedStudents_RefusesWithCount()
    {
        var (ctx, section) = await SeedSectionAsync(10,
            EnrollmentStatus.Enrolled, EnrollmentStatus.Approved, EnrollmentStatus.Cancelled);

        var handler = new DeleteSectionCommandHandler(ctx);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new DeleteSectionCommand(section.Id), default));

        Assert.Equal("Section has 2 enrolled students — reassign them first.", ex.Message);
        Assert.Single(await ctx.Sections.ToListAsync()); // still there
    }

    [Fact]
    public async Task DeleteSection_OnlyCancelledEnrollments_Deletes()
    {
        var (ctx, section) = await SeedSectionAsync(10, EnrollmentStatus.Cancelled);

        var handler = new DeleteSectionCommandHandler(ctx);
        await handler.Handle(new DeleteSectionCommand(section.Id), default);

        Assert.Empty(await ctx.Sections.ToListAsync());
    }

    [Fact]
    public async Task DeleteSection_NotFound_Throws()
    {
        var ctx = TestDb.Create();

        var handler = new DeleteSectionCommandHandler(ctx);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.Handle(new DeleteSectionCommand(Guid.NewGuid()), default));
    }
}
