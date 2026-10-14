using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class ApprovalLearnerRecordTests
{
    private static readonly DateTime Dob = new(2009, 3, 10);

    private static AdmissionApplication App(string first = "Ana", string? lrn = "136512260001") => new()
    {
        ApplicationNumber = $"APP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
        FirstName = first, LastName = "Cruz", Email = "", Gender = "Female", DateOfBirth = Dob,
        GradeLevel = "Grade 11", SchoolYear = "2026-2027", Status = "Submitted",
        ApplicationType = "Parent", ParentUserId = Guid.NewGuid(),
        Lrn = lrn, LearnerType = "Transferee", PreviousSchool = "Rizal HS", PreviousSchoolAddress = "Pasig",
        LastGradeLevelCompleted = "Grade 10", LastSchoolYearCompleted = "2025-2026", LastSchoolId = "305123",
        Track = "Academic", Strand = "STEM",
        PsaBirthCertificateNumber = "PSA-9", Religion = "Catholic", MotherTongue = "Tagalog",
        HomeAddress = new Domain.ValueObjects.PsgcAddress
        {
            RegionCode = "1300000000", RegionName = "NCR",
            CityMunicipalityCode = "1381300000", CityMunicipalityName = "Quezon City",
            BarangayCode = "1381300001", BarangayName = "Alicia"
        }
    };

    private static Task ApproveAsync(ApplicationDbContext ctx, Guid appId) =>
        new ReviewApplicationCommandHandler(ctx, new RecordingEmailSender())
            .Handle(new ReviewApplicationCommand(appId, IsApproved: true, null), default);

    [Fact]
    public async Task NewLearner_GetsProfileLrnStudentNumberAndEnrollmentInfo()
    {
        var ctx = TestDb.Create();
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        var s = await ctx.Students.SingleAsync();
        Assert.Equal("136512260001", s.LRN);
        Assert.False(s.LrnNeedsReview);
        Assert.Matches("^[0-9]{4}-[0-9]{5}$", s.StudentNumber);
        Assert.Equal("PSA-9", s.PsaBirthCertificateNumber);
        Assert.Equal("1381300001", s.HomeAddress.BarangayCode);

        var e = await ctx.Enrollments.SingleAsync();
        Assert.Equal("Transferee", e.LearnerType);
        Assert.Equal("Rizal HS", e.LastSchoolAttended);
        Assert.Equal("Pasig", e.LastSchoolAddress);
        Assert.Equal("305123", e.LastSchoolId);
        Assert.Equal("STEM", e.Strand);
    }

    [Fact]
    public async Task MatchesByLrnColumn_EvenWhenTheNameDiffers()
    {
        var ctx = TestDb.Create();
        var existing = new Student { StudentNumber = "2025-00001", LRN = "136512260001", FirstName = "Ana Marie", LastName = "Cruz", BirthDate = Dob, Address = "" };
        ctx.Students.Add(existing);
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        Assert.Equal(1, await ctx.Students.CountAsync());
        Assert.Equal(existing.Id, (await ctx.Enrollments.SingleAsync()).StudentId);
    }

    [Fact]
    public async Task NameDobCandidate_WithADifferentLrn_IsADifferentLearner()
    {
        var ctx = TestDb.Create();
        ctx.Students.Add(new Student { StudentNumber = "2025-00001", LRN = "999999999999", FirstName = "Ana", LastName = "Cruz", BirthDate = Dob, Address = "" });
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        Assert.Equal(2, await ctx.Students.CountAsync());
    }

    [Fact]
    public async Task MatchedStudent_GetsBlanksFilled_NeverOverwritten_AndAdoptsLrnWhenBlank()
    {
        var ctx = TestDb.Create();
        var existing = new Student
        {
            StudentNumber = "2025-00001", LRN = null, FirstName = "Ana", LastName = "Cruz", BirthDate = Dob, Address = "",
            Religion = "INC"
        };
        ctx.Students.Add(existing);
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        var s = await ctx.Students.SingleAsync();
        Assert.Equal("INC", s.Religion);                 // kept
        Assert.Equal("Tagalog", s.MotherTongue);         // filled
        Assert.Equal("136512260001", s.LRN);             // adopted
        Assert.Equal("1381300001", s.HomeAddress.BarangayCode);
    }

    [Fact]
    public async Task LrnHeldByAnInactiveStudent_FailsCleanly_AndNothingPersists()
    {
        var dbName = $"approval-lrn-{Guid.NewGuid()}";
        Guid appId;
        using (var seed = TestDb.Create(dbName))
        {
            seed.Students.Add(new Student { StudentNumber = "2024-00007", LRN = "136512260001", FirstName = "Old", LastName = "Record", BirthDate = Dob, Address = "", IsActive = false });
            var app = App(first: "Zed");
            seed.AdmissionApplications.Add(app);
            await seed.SaveChangesAsync();
            appId = app.Id;
        }

        using (var ctx = TestDb.Create(dbName))
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ApproveAsync(ctx, appId));
            Assert.Contains("2024-00007", ex.Message);
        }

        using var verify = TestDb.Create(dbName);
        Assert.Equal("Submitted", (await verify.AdmissionApplications.SingleAsync()).Status);
        Assert.Equal(1, await verify.Students.CountAsync());
    }

    [Fact]
    public async Task MatchedStudent_GetsTheirVerifiedPsaCarriedOver()
    {
        var ctx = TestDb.Create();
        ctx.RequirementTemplates.Add(new RequirementTemplate { DocumentName = "PSA Birth Certificate", IsActive = true, KeepOnFile = true });
        var existing = new Student { StudentNumber = "2025-00001", LRN = "136512260001", FirstName = "Ana", LastName = "Cruz", BirthDate = Dob, Address = "" };
        ctx.Students.Add(existing);
        var prior = new Enrollment { StudentId = existing.Id, SchoolYear = "2025-2026", GradeLevel = "Grade 10", Status = EnrollmentStatus.Enrolled };
        ctx.Enrollments.Add(prior);
        ctx.EnrollmentRequirements.Add(new EnrollmentRequirement
        {
            EnrollmentId = prior.Id, DocumentName = "PSA Birth Certificate", IsSubmitted = true, IsVerified = true,
            FileName = "psa.pdf", VerifiedAt = new DateTime(2025, 6, 3)
        });
        var app = App();
        ctx.AdmissionApplications.Add(app);
        await ctx.SaveChangesAsync();

        await ApproveAsync(ctx, app.Id);

        var created = await ctx.Enrollments.SingleAsync(e => e.SchoolYear == "2026-2027");
        Assert.True((await ctx.EnrollmentRequirements.SingleAsync(r => r.EnrollmentId == created.Id)).IsVerified);
    }
}
