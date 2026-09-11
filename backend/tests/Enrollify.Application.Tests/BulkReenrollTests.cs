using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class BulkReenrollTests
{
    private const string FromYear = "2025-2026";
    private const string ToYear = "2026-2027";

    private static Student NewStudent(string lrn, string lastName) => new()
    {
        LRN = lrn, FirstName = "Kid", LastName = lastName, Address = "Manila"
    };

    private static Enrollment EnrollmentOf(Guid studentId, string year, string grade, EnrollmentStatus status) => new()
    {
        StudentId = studentId, SchoolYear = year, GradeLevel = grade, Status = status
    };

    private static async Task SeedScenarioAsync(ApplicationDbContext ctx)
    {
        // The target year must exist — the handler refuses to re-enroll into a typo'd year.
        ctx.SchoolYears.Add(new SchoolYear
        {
            Name = ToYear,
            StartDate = new DateTime(2026, 6, 1),
            EndDate = new DateTime(2027, 3, 31),
            IsActive = false
        });

        var a = NewStudent("LRN-0901", "Alpha");   // Grade 7 Enrolled → re-enrolls as Grade 8
        var b = NewStudent("LRN-0902", "Bravo");   // Grade 12 Enrolled → graduate, skipped
        var c = NewStudent("LRN-0903", "Charlie"); // Enrolled but already has a ToYear enrollment → skipped
        var d = NewStudent("LRN-0904", "Delta");   // only Assessed in FromYear → not eligible at all
        ctx.Students.AddRange(a, b, c, d);

        ctx.Enrollments.Add(EnrollmentOf(a.Id, FromYear, "Grade 7", EnrollmentStatus.Enrolled));
        ctx.Enrollments.Add(EnrollmentOf(b.Id, FromYear, "Grade 12", EnrollmentStatus.Enrolled));
        ctx.Enrollments.Add(EnrollmentOf(c.Id, FromYear, "Grade 8", EnrollmentStatus.Enrolled));
        ctx.Enrollments.Add(EnrollmentOf(c.Id, ToYear, "Grade 9", EnrollmentStatus.Draft));
        ctx.Enrollments.Add(EnrollmentOf(d.Id, FromYear, "Grade 7", EnrollmentStatus.Assessed));

        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "PSA Birth Certificate", IsActive = true, DisplayOrder = 1 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Report Card", IsActive = true, DisplayOrder = 2 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Grade 8 Assessment", GradeLevel = "Grade 8", IsActive = true, DisplayOrder = 3 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Retired Doc", IsActive = false, DisplayOrder = 4 });

        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task BulkReenroll_CreatesPromotedDrafts_WithTemplateRequirementsAndHistory()
    {
        var ctx = TestDb.Create();
        await SeedScenarioAsync(ctx);

        var result = await new BulkReenrollCommandHandler(ctx)
            .Handle(new BulkReenrollCommand(FromYear, ToYear), default);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.SkippedExisting);
        Assert.Equal(1, result.SkippedGraduates);

        var alpha = await ctx.Students.SingleAsync(s => s.LastName == "Alpha");
        var created = await ctx.Enrollments
            .SingleAsync(e => e.StudentId == alpha.Id && e.SchoolYear == ToYear);
        Assert.Equal("Grade 8", created.GradeLevel); // promoted from Grade 7
        Assert.Equal(EnrollmentStatus.Draft, created.Status);

        var requirements = await ctx.EnrollmentRequirements
            .Where(r => r.EnrollmentId == created.Id)
            .Select(r => r.DocumentName)
            .ToListAsync();
        Assert.Equal(3, requirements.Count); // 2 generic + the Grade 8 one; inactive excluded
        Assert.Contains("Grade 8 Assessment", requirements);
        Assert.DoesNotContain("Retired Doc", requirements);

        var history = await ctx.EnrollmentStatusHistories.SingleAsync(h => h.EnrollmentId == created.Id);
        Assert.Equal($"Bulk re-enrolled from {FromYear}", history.Remarks);

        // Charlie's existing ToYear enrollment is untouched and not duplicated.
        var charlie = await ctx.Students.SingleAsync(s => s.LastName == "Charlie");
        Assert.Equal(1, await ctx.Enrollments.CountAsync(e => e.StudentId == charlie.Id && e.SchoolYear == ToYear));
    }

    [Fact]
    public async Task BulkReenroll_FailedSave_LeavesNothingBehind()
    {
        var dbName = $"bulk-reenroll-{Guid.NewGuid()}";
        using (var seedCtx = TestDb.Create(dbName))
        {
            await SeedScenarioAsync(seedCtx);
        }

        using (var failing = new FailingSaveContext(TestDb.Options(dbName), new FixedTenantProvider(TestDb.TenantId)) { Fail = true })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new BulkReenrollCommandHandler(failing).Handle(new BulkReenrollCommand(FromYear, ToYear), default));
        }

        using var verify = TestDb.Create(dbName);
        var alpha = await verify.Students.SingleAsync(s => s.LastName == "Alpha");
        Assert.Equal(0, await verify.Enrollments.CountAsync(e => e.StudentId == alpha.Id && e.SchoolYear == ToYear));
        Assert.Empty(await verify.EnrollmentRequirements.ToListAsync());
        Assert.Empty(await verify.EnrollmentStatusHistories.ToListAsync());
    }

    [Fact]
    public async Task BulkReenroll_UnknownTargetYear_Throws()
    {
        var ctx = TestDb.Create();
        await SeedScenarioAsync(ctx);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new BulkReenrollCommandHandler(ctx).Handle(new BulkReenrollCommand(FromYear, "2026-2028"), default));

        Assert.Contains("'2026-2028' does not exist", ex.Message);
        Assert.Equal(0, await ctx.Enrollments.CountAsync(e => e.SchoolYear == "2026-2028"));
    }

    [Fact]
    public void Validator_RejectsSameSourceAndTargetYear()
    {
        var validator = new BulkReenrollCommandValidator();

        Assert.False(validator.Validate(new BulkReenrollCommand(FromYear, FromYear)).IsValid);
        Assert.False(validator.Validate(new BulkReenrollCommand("", ToYear)).IsValid);
        Assert.True(validator.Validate(new BulkReenrollCommand(FromYear, ToYear)).IsValid);
    }
}
