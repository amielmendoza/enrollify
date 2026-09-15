using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

/// <summary>Approval must reuse an existing active student instead of minting a duplicate.</summary>
public class StudentDedupTests
{
    private static readonly DateTime Dob = new(2012, 3, 10);

    private static Student ExistingStudent(string lrn = "100100100001", string first = "Mika", string last = "Velasco",
        bool isActive = true, Guid? parentUserId = null) => new()
    {
        LRN = lrn, FirstName = first, LastName = last,
        BirthDate = Dob, Address = "Manila", IsActive = isActive, ParentUserId = parentUserId
    };

    private static AdmissionApplication ParentApp(Guid parentUserId, string first = "Mika", string last = "Velasco",
        string? customJson = null) => new()
    {
        ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
        FirstName = first, LastName = last,
        Email = "kid@example.com", Gender = "Female",
        DateOfBirth = Dob,
        GradeLevel = "Grade 8", SchoolYear = "2026-2027",
        Status = "Submitted",
        ApplicationType = "Parent",
        ParentUserId = parentUserId, // authenticated parent → no new user account created
        CustomFieldValues = customJson
    };

    private static Task<Application.DTOs.Admissions.ApplicationDetailDto> ApproveAsync(ApplicationDbContext ctx, Guid appId) =>
        new ReviewApplicationCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewApplicationCommand(appId, IsApproved: true, null), default);

    [Fact]
    public async Task Approval_MatchesByNameAndDob_CaseInsensitively_AndReusesStudent()
    {
        var ctx = TestDb.Create();
        var parentId = Guid.NewGuid();
        var existing = ExistingStudent(); // no ParentUserId yet
        ctx.Students.Add(existing);
        var app = ParentApp(parentId, first: "MIKA", last: "VELASCO"); // different casing
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        var dto = await ApproveAsync(ctx, app.Id);

        Assert.Equal(existing.Id, dto.StudentId);                    // linked, not duplicated
        Assert.Equal(1, await ctx.Students.CountAsync());            // no second Student/LRN
        Assert.Equal(parentId, existing.ParentUserId);               // account attached (was null)

        var enrollment = await ctx.Enrollments.SingleAsync();
        Assert.Equal(existing.Id, enrollment.StudentId);
        Assert.Equal("2026-2027", enrollment.SchoolYear);
        Assert.Contains($"existing student {existing.LRN}", enrollment.Remarks);
    }

    [Fact]
    public async Task Approval_MatchesByProvidedLrn_FromCustomField()
    {
        var ctx = TestDb.Create();
        // Different name and the LRN wins the match.
        var existing = ExistingStudent(lrn: "L-777", first: "Maria Mikaela");
        ctx.Students.Add(existing);
        var app = ParentApp(Guid.NewGuid(), customJson: "{\"lrn\":\" L-777 \"}");
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        var dto = await ApproveAsync(ctx, app.Id);

        Assert.Equal(existing.Id, dto.StudentId);
        Assert.Equal(1, await ctx.Students.CountAsync());
    }

    [Fact]
    public async Task Approval_MatchedStudentAlreadyEnrolledForYear_FailsCleanly()
    {
        var dbName = $"dedup-conflict-{Guid.NewGuid()}";
        Guid appId;
        using (var seed = TestDb.Create(dbName))
        {
            var existing = ExistingStudent();
            seed.Students.Add(existing);
            seed.Enrollments.Add(new Enrollment
            {
                StudentId = existing.Id, SchoolYear = "2026-2027", GradeLevel = "Grade 8",
                Status = EnrollmentStatus.Draft
            });
            var app = ParentApp(Guid.NewGuid());
            seed.AdmissionApplications.Add(app);
            await seed.SaveChangesAsync();
            appId = app.Id;
        }

        using (var ctx = TestDb.Create(dbName))
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ApproveAsync(ctx, appId));
            Assert.Contains("already enrolled for 2026-2027", ex.Message);
            Assert.Contains("cancel the existing enrollment first", ex.Message);
        }

        // Nothing persisted: the application is still reviewable.
        using var verify = TestDb.Create(dbName);
        Assert.Equal("Submitted", (await verify.AdmissionApplications.SingleAsync(a => a.Id == appId)).Status);
        Assert.Equal(1, await verify.Students.CountAsync());
        Assert.Equal(1, await verify.Enrollments.CountAsync());
    }

    [Fact]
    public async Task Approval_NeverMatchesInactiveStudents()
    {
        var ctx = TestDb.Create();
        ctx.Students.Add(ExistingStudent(isActive: false));
        var app = ParentApp(Guid.NewGuid());
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        // A deactivated record must not resurrect through admissions — a new student is minted.
        Assert.Equal(2, await ctx.Students.CountAsync());
    }
}
