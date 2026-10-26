using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Application.Features.Students;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class GuardianRelationshipApprovalTests
{
    private static Student NewStudent(string? relationship = null) => new()
    {
        StudentNumber = $"2026-{Random.Shared.Next(10000, 99999)}",
        FirstName = "Ana", LastName = "Cruz", BirthDate = new DateTime(2009, 3, 10),
        Address = "1 Rizal St", GuardianRelationship = relationship
    };

    private static AdmissionApplication NewApp(Guid studentId, string status, string? relationship, DateTime updatedAt) => new()
    {
        ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
        FirstName = "Ana", LastName = "Cruz", Email = "", Gender = "Female",
        DateOfBirth = new DateTime(2009, 3, 10), GradeLevel = "Grade 11", SchoolYear = "2026-2027",
        Status = status, StudentId = studentId, GuardianRelationship = relationship, UpdatedAt = updatedAt
    };

    [Fact]
    public async Task Backfill_copies_relationship_from_latest_approved_application()
    {
        var ctx = TestDb.Create();
        var student = NewStudent();
        ctx.Students.Add(student);
        ctx.AdmissionApplications.Add(NewApp(student.Id, "Approved", "Mother", new DateTime(2026, 6, 2)));
        ctx.AdmissionApplications.Add(NewApp(student.Id, "Approved", "Father", new DateTime(2026, 5, 1)));
        await ctx.SaveChangesAsync();

        var touched = await GuardianRelationshipBackfill.RunForTenantAsync(ctx, TestDb.TenantId, default);
        await ctx.SaveChangesAsync();

        Assert.Equal(1, touched);
        Assert.Equal("Mother", (await ctx.Students.SingleAsync()).GuardianRelationship);
    }

    [Fact]
    public async Task Backfill_skips_blank_and_unapproved_applications_and_keeps_existing_values()
    {
        var ctx = TestDb.Create();
        var blankFirst = NewStudent();
        var onlyPending = NewStudent();
        var alreadySet = NewStudent("Aunt");
        ctx.Students.AddRange(blankFirst, onlyPending, alreadySet);
        // Newest approved is blank, so the older non-blank one wins.
        ctx.AdmissionApplications.Add(NewApp(blankFirst.Id, "Approved", "  ", new DateTime(2026, 6, 2)));
        ctx.AdmissionApplications.Add(NewApp(blankFirst.Id, "Approved", "Father", new DateTime(2026, 5, 1)));
        ctx.AdmissionApplications.Add(NewApp(onlyPending.Id, "Submitted", "Mother", new DateTime(2026, 6, 2)));
        ctx.AdmissionApplications.Add(NewApp(alreadySet.Id, "Approved", "Mother", new DateTime(2026, 6, 2)));
        await ctx.SaveChangesAsync();

        await GuardianRelationshipBackfill.RunForTenantAsync(ctx, TestDb.TenantId, default);
        await ctx.SaveChangesAsync();

        Assert.Equal("Father", (await ctx.Students.SingleAsync(s => s.Id == blankFirst.Id)).GuardianRelationship);
        Assert.Null((await ctx.Students.SingleAsync(s => s.Id == onlyPending.Id)).GuardianRelationship);
        Assert.Equal("Aunt", (await ctx.Students.SingleAsync(s => s.Id == alreadySet.Id)).GuardianRelationship);
    }

    [Fact]
    public async Task Backfill_is_idempotent()
    {
        var ctx = TestDb.Create();
        var student = NewStudent();
        ctx.Students.Add(student);
        ctx.AdmissionApplications.Add(NewApp(student.Id, "Approved", "Mother", new DateTime(2026, 6, 2)));
        await ctx.SaveChangesAsync();

        await GuardianRelationshipBackfill.RunForTenantAsync(ctx, TestDb.TenantId, default);
        await ctx.SaveChangesAsync();
        var second = await GuardianRelationshipBackfill.RunForTenantAsync(ctx, TestDb.TenantId, default);

        Assert.Equal(0, second);
    }

    private static AdmissionApplication ApprovableApp(string? relationship) => new()
    {
        ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
        FirstName = "Ana", LastName = "Cruz", Email = "", Gender = "Female",
        DateOfBirth = new DateTime(2009, 3, 10), GradeLevel = "Grade 11", SchoolYear = "2026-2027",
        Status = "Submitted", ApplicationType = "Parent", ParentUserId = Guid.NewGuid(),
        GuardianRelationship = relationship
    };

    private static Task ApproveAsync(Enrollify.Infrastructure.Persistence.ApplicationDbContext ctx, Guid appId) =>
        new ReviewApplicationCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewApplicationCommand(appId, IsApproved: true, null), default);

    [Fact]
    public async Task Approval_copies_relationship_onto_a_new_student()
    {
        var ctx = TestDb.Create();
        var app = ApprovableApp("Mother");
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        Assert.Equal("Mother", (await ctx.Students.SingleAsync()).GuardianRelationship);
    }

    [Fact]
    public async Task Approval_fills_a_reused_students_empty_relationship()
    {
        var ctx = TestDb.Create();
        var existing = NewStudent();
        ctx.Students.Add(existing);
        var app = ApprovableApp("Father");
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        var student = await ctx.Students.SingleAsync();
        Assert.Equal(existing.Id, student.Id);
        Assert.Equal("Father", student.GuardianRelationship);
    }

    [Fact]
    public async Task Approval_fills_a_reused_students_blank_relationship()
    {
        var ctx = TestDb.Create();
        var existing = NewStudent("   ");
        ctx.Students.Add(existing);
        var app = ApprovableApp("Father");
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        Assert.Equal("Father", (await ctx.Students.SingleAsync()).GuardianRelationship);
    }

    [Fact]
    public async Task Approval_preserves_a_reused_students_existing_relationship()
    {
        var ctx = TestDb.Create();
        var existing = NewStudent("Aunt");
        ctx.Students.Add(existing);
        var app = ApprovableApp("Mother");
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        var student = await ctx.Students.SingleAsync();
        Assert.Equal(existing.Id, student.Id);
        Assert.Equal("Aunt", student.GuardianRelationship);
    }
}
