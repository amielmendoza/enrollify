using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Application.Features.ApplicationFormFields;
using Enrollify.Domain.Entities;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class SubmitApplicationLearnerTests
{
    private static SubmitApplicationCommand.Applicant Child(
        string first = "Ana", string grade = "Grade 11", string? lrn = null, LearnerProfileDto? learner = null,
        string? track = null, string? strand = null, string? lastSchoolId = null) => new(
        first, null, "Cruz", "", null, "Female", new DateTime(2009, 3, 10), null,
        grade, "2026-2027", null, null, null, null, "Mother", null,
        Lrn: lrn, Learner: learner, LearnerType: "Transferee", LastSchoolId: lastSchoolId, Track: track, Strand: strand);

    private static SubmitApplicationCommand Parent(params SubmitApplicationCommand.Applicant[] children) => new(
        null, "Parent", "Rosa", "Cruz", "rosa@example.com", "0917-555-0000", children.ToList(), TestDb.TenantId);

    private static async Task<ApplicationDbContext> SeedAsync(bool strandActive = true)
    {
        var ctx = TestDb.Create();
        await DefaultApplicationFormFields.EnsureFieldsForTenantAsync(ctx, TestDb.TenantId);
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = strandActive });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public void Validator_RejectsMalformedLrn()
    {
        var result = new SubmitApplicationCommandValidator().Validate(Parent(Child(lrn: "12345")));
        Assert.Contains(result.Errors, e => e.PropertyName.EndsWith("Lrn"));
    }

    [Fact]
    public void Validator_ChecksEnrollmentFields_UnderAnEnrollmentPath()
    {
        var result = new SubmitApplicationCommandValidator().Validate(Parent(Child(lastSchoolId: "12")));
        Assert.Contains(result.Errors, e => e.PropertyName.EndsWith("Enrollment.LastSchoolId"));
    }

    [Fact]
    public void Validator_ChecksNestedLearnerProfile()
    {
        var result = new SubmitApplicationCommandValidator().Validate(Parent(Child(learner: new LearnerProfileDto(IsIndigenousPeople: true))));
        Assert.Contains(result.Errors, e => e.PropertyName.EndsWith("IpCommunity"));
    }

    [Fact]
    public async Task Siblings_OnlyTheShsChildNeedsAStrand_AndTheErrorNamesThatChild()
    {
        var ctx = await SeedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child("Ana", "Grade 11"), Child("Ben", "Grade 3")), default));

        Assert.Contains("Ana (Grade 11)", ex.Message);
        Assert.Equal(0, await ctx.AdmissionApplications.CountAsync());
    }

    [Fact]
    public async Task Strand_IsNormalizedAndPersisted()
    {
        var ctx = await SeedAsync();
        await new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child(track: "academic", strand: "stem")), default);

        var app = await ctx.AdmissionApplications.SingleAsync();
        Assert.Equal("Academic", app.Track);
        Assert.Equal("STEM", app.Strand);
    }

    [Fact]
    public async Task StrandOnNonShsGrade_IsRefused_NamingTheChild()
    {
        var ctx = await SeedAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child("Ben", "Grade 3", track: "Academic", strand: "STEM")), default));
        Assert.StartsWith("Ben:", ex.Message);
    }

    [Fact]
    public async Task SchoolWithNoActiveStrands_AcceptsGrade11WithoutStrand()
    {
        var ctx = await SeedAsync(strandActive: false);
        await new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child()), default);
        Assert.Equal(1, await ctx.AdmissionApplications.CountAsync());
    }

    [Fact]
    public async Task LearnerFields_ArePersisted_AndReturnedInTheDetailDto()
    {
        var ctx = await SeedAsync();
        var learner = new LearnerProfileDto(
            PsaBirthCertificateNumber: "PSA-9", Religion: "Catholic", Is4PsBeneficiary: true, FourPsHouseholdId: "HH-7",
            FatherLastName: "Cruz", FatherFirstName: "Jun",
            HomeAddress: new PsgcAddressDto("1300000000", "NCR", null, null, "1381300000", "Quezon City", "1381300001", "Alicia", "1105"));

        var result = await new SubmitApplicationCommandHandler(ctx).Handle(
            Parent(Child(lrn: "136512260001", learner: learner, track: "Academic", strand: "STEM")), default);

        var dto = Assert.Single(result);
        Assert.Equal("136512260001", dto.Lrn);
        Assert.Equal("HH-7", dto.Learner!.FourPsHouseholdId);
        Assert.Equal("1381300001", dto.Learner.HomeAddress!.BarangayCode);
        Assert.Equal("Transferee", dto.EnrollmentInfo!.LearnerType);
        Assert.Equal("STEM", dto.EnrollmentInfo.Strand);
    }

    [Fact]
    public async Task AdminRequiredLrn_IsEnforced()
    {
        var ctx = await SeedAsync();
        var field = await ctx.ApplicationFormFields.SingleAsync(f => f.FieldKey == "lrn");
        field.IsRequired = true;
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SubmitApplicationCommandHandler(ctx).Handle(Parent(Child(track: "Academic", strand: "STEM")), default));
        Assert.Contains("is required", ex.Message);
    }

    [Fact]
    public async Task DefaultFormFields_IncludeTheDepEdKeys()
    {
        var ctx = await SeedAsync();
        var keys = await ctx.ApplicationFormFields.Select(f => f.FieldKey).ToListAsync();
        foreach (var k in new[] { "lrn", "psaBirthCertificateNumber", "indigenousPeople", "fourPs", "disability",
                     "fatherName", "motherMaidenName", "learnerType", "lastSchoolId", "strand" })
            Assert.Contains(k, keys);
        Assert.Equal("Family", (await ctx.ApplicationFormFields.SingleAsync(f => f.FieldKey == "fatherName")).Section);
    }
}
