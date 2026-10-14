using Enrollify.Application.Features.Enrollments;
using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class EnrollmentRequirementsTests
{
    private static readonly DateTime VerifiedOn = new(2025, 6, 3, 2, 0, 0, DateTimeKind.Utc);

    private static async Task<(ApplicationDbContext Ctx, Student Student)> SeedAsync(
        bool verified = true, EnrollmentStatus priorStatus = EnrollmentStatus.Enrolled, string priorDocName = "PSA Birth Certificate")
    {
        var ctx = TestDb.Create();
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "PSA Birth Certificate", IsActive = true, KeepOnFile = true, DisplayOrder = 1 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Form 138", IsActive = true, DisplayOrder = 2 });
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "Grade 11 Waiver", GradeLevel = "Grade 11", IsActive = true, DisplayOrder = 3 });

        var student = new Student { StudentNumber = "2025-00001", FirstName = "Ana", LastName = "Cruz", Address = "" };
        ctx.Students.Add(student);
        var prior = new Enrollment { StudentId = student.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = priorStatus };
        ctx.Enrollments.Add(prior);
        ctx.EnrollmentRequirements.Add(new EnrollmentRequirement
        {
            EnrollmentId = prior.Id, DocumentName = priorDocName, IsSubmitted = true, IsVerified = verified,
            FileName = "psa.pdf", Notes = "/api/files/abc", VerifiedBy = verified ? "registrar@mshs.edu.ph" : null,
            VerifiedAt = verified ? VerifiedOn : null
        });
        ctx.EnrollmentRequirements.Add(new EnrollmentRequirement
        {
            EnrollmentId = prior.Id, DocumentName = "Form 138", IsSubmitted = true, IsVerified = true, FileName = "f138.pdf"
        });
        await ctx.SaveChangesAsync();
        return (ctx, student);
    }

    private static async Task<List<EnrollmentRequirement>> AddForNewAsync(ApplicationDbContext ctx, Student student, string grade = "Grade 8")
    {
        var next = new Enrollment { StudentId = student.Id, SchoolYear = "2026-2027", GradeLevel = grade, Status = EnrollmentStatus.Draft };
        ctx.Enrollments.Add(next);
        await EnrollmentRequirements.AddForAsync(ctx, next, default);
        await ctx.SaveChangesAsync();
        return await ctx.EnrollmentRequirements.Where(r => r.EnrollmentId == next.Id).OrderBy(r => r.DocumentName).ToListAsync();
    }

    [Fact]
    public async Task VerifiedKeepOnFileDocument_IsCarriedOver()
    {
        var (ctx, student) = await SeedAsync();
        var reqs = await AddForNewAsync(ctx, student);

        var psa = reqs.Single(r => r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsSubmitted);
        Assert.True(psa.IsVerified);
        Assert.Equal("psa.pdf", psa.FileName);
        Assert.Equal("/api/files/abc", psa.Notes);
        Assert.Equal("registrar@mshs.edu.ph", psa.VerifiedBy);
        Assert.Equal("On file — verified Jun 3, 2025 (SY 2025-2026)", psa.ReviewNotes);
    }

    [Fact]
    public async Task NonKeepOnFileDocument_StartsFresh_EvenIfVerifiedBefore()
    {
        var (ctx, student) = await SeedAsync();
        var reqs = await AddForNewAsync(ctx, student);

        var f138 = reqs.Single(r => r.DocumentName == "Form 138");
        Assert.False(f138.IsSubmitted);
        Assert.Null(f138.FileName);
    }

    [Fact]
    public async Task UnverifiedCopy_IsNotCarried()
    {
        var (ctx, student) = await SeedAsync(verified: false);
        var reqs = await AddForNewAsync(ctx, student);
        Assert.False(reqs.Single(r => r.DocumentName == "PSA Birth Certificate").IsSubmitted);
    }

    [Fact]
    public async Task CopyFromCancelledEnrollment_IsStillCarried()
    {
        var (ctx, student) = await SeedAsync(priorStatus: EnrollmentStatus.Cancelled);
        var reqs = await AddForNewAsync(ctx, student);
        Assert.True(reqs.Single(r => r.DocumentName == "PSA Birth Certificate").IsVerified);
    }

    [Fact]
    public async Task DocumentNames_MatchIgnoringCaseAndWhitespace()
    {
        var (ctx, student) = await SeedAsync(priorDocName: "  psa birth certificate ");
        var reqs = await AddForNewAsync(ctx, student);
        Assert.True(reqs.Single(r => r.DocumentName == "PSA Birth Certificate").IsVerified);
    }

    [Fact]
    public async Task GradeSpecificTemplates_StillFilterByGrade()
    {
        var (ctx, student) = await SeedAsync();
        Assert.DoesNotContain(await AddForNewAsync(ctx, student, "Grade 8"), r => r.DocumentName == "Grade 11 Waiver");
    }

    [Fact]
    public async Task MostRecentVerifiedCopy_Wins()
    {
        var (ctx, student) = await SeedAsync();
        var later = new Enrollment { StudentId = student.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 7", Status = EnrollmentStatus.Cancelled };
        ctx.Enrollments.Add(later);
        ctx.EnrollmentRequirements.Add(new EnrollmentRequirement
        {
            EnrollmentId = later.Id, DocumentName = "PSA Birth Certificate", IsSubmitted = true, IsVerified = true,
            FileName = "psa-v2.pdf", VerifiedBy = "admin@mshs.edu.ph", VerifiedAt = VerifiedOn.AddMonths(1)
        });
        await ctx.SaveChangesAsync();

        var reqs = await AddForNewAsync(ctx, student);
        Assert.Equal("psa-v2.pdf", reqs.Single(r => r.DocumentName == "PSA Birth Certificate").FileName);
    }

    [Fact]
    public async Task StaffCreateEnrollment_UsesTheBuilder()
    {
        var (ctx, student) = await SeedAsync();
        var dto = await new CreateEnrollmentCommandHandler(ctx).Handle(new CreateEnrollmentCommand(student.Id, "2026-2027", "Grade 8"), default);

        var psa = await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == dto.Id && r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsVerified);
    }

    [Fact]
    public async Task BulkReenroll_UsesTheBuilder()
    {
        var (ctx, student) = await SeedAsync();
        ctx.SchoolYears.Add(new SchoolYear { Name = "2026-2027", StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        await ctx.SaveChangesAsync();

        await new BulkReenrollCommandHandler(ctx).Handle(new BulkReenrollCommand("2025-2026", "2026-2027"), default);

        var created = await ctx.Enrollments.SingleAsync(e => e.SchoolYear == "2026-2027");
        var psa = await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == created.Id && r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsVerified);
    }

    [Fact]
    public async Task ParentRequest_UsesTheBuilder()
    {
        var (ctx, student) = await SeedAsync();
        var parentId = Guid.NewGuid();
        student.ParentUserId = parentId;
        ctx.SchoolYears.Add(new SchoolYear { Name = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        await ctx.SaveChangesAsync();

        var dto = await new RequestEnrollmentCommandHandler(ctx).Handle(new RequestEnrollmentCommand(student.Id, parentId), default);

        var psa = await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == dto.Id && r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsVerified);
    }

    [Fact]
    public async Task StudentRequest_UsesTheBuilder()
    {
        var (ctx, student) = await SeedAsync();
        var userId = Guid.NewGuid();
        student.UserId = userId;
        ctx.SchoolYears.Add(new SchoolYear { Name = "2026-2027", IsActive = true, StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2027, 3, 31) });
        await ctx.SaveChangesAsync();

        var dto = await new StudentRequestEnrollmentCommandHandler(ctx).Handle(new StudentRequestEnrollmentCommand(userId), default);

        var psa = await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == dto.Id && r.DocumentName == "PSA Birth Certificate");
        Assert.True(psa.IsVerified);
    }
}
