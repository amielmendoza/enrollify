using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Students;
using Enrollify.Application.Features.Students.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.ValueObjects;
using Xunit;

namespace Enrollify.Application.Tests;

public class StudentListLearnerTests
{
    private static Student S(string number, string first, string? lrn = null, bool flagged = false) => new()
    {
        StudentNumber = number, FirstName = first, LastName = "Cruz", Address = "", LRN = lrn, LrnNeedsReview = flagged
    };

    [Fact]
    public async Task Search_WithNullLrnStudents_FindsByStudentNumber()
    {
        var ctx = TestDb.Create();
        ctx.Students.AddRange(S("2026-00001", "Ana"), S("2026-00002", "Ben", "136512260001"));
        await ctx.SaveChangesAsync();

        var page = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery("2026-00001"), default);

        Assert.Equal("Ana", Assert.Single(page.Items).FirstName);
    }

    [Fact]
    public async Task Search_FindsByLrn()
    {
        var ctx = TestDb.Create();
        ctx.Students.AddRange(S("2026-00001", "Ana"), S("2026-00002", "Ben", "136512260001"));
        await ctx.SaveChangesAsync();

        var page = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery("1365122600"), default);

        Assert.Equal("Ben", Assert.Single(page.Items).FirstName);
    }

    [Fact]
    public async Task MissingLrnFilter_ReturnsOnlyStudentsWithoutLrn()
    {
        var ctx = TestDb.Create();
        ctx.Students.AddRange(S("2026-00001", "Ana"), S("2026-00002", "Ben", "136512260001"));
        await ctx.SaveChangesAsync();

        var page = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery(null, MissingLrn: true), default);

        Assert.Equal("Ana", Assert.Single(page.Items).FirstName);
    }

    [Fact]
    public async Task LrnNeedsReviewFilter_ReturnsOnlyFlagged()
    {
        var ctx = TestDb.Create();
        ctx.Students.AddRange(S("2026-00001", "Ana", "100100100001", flagged: true), S("2026-00002", "Ben", "136512260001"));
        await ctx.SaveChangesAsync();

        var page = await new GetStudentsQueryHandler(ctx).Handle(new GetStudentsQuery(null, LrnNeedsReview: true), default);

        var only = Assert.Single(page.Items);
        Assert.True(only.LrnNeedsReview);
        Assert.Equal("2026-00001", only.StudentNumber);
    }

    [Fact]
    public void StudentMapping_ExposesProfileAndSplitsDisabilities()
    {
        var s = S("2026-00009", "Cy");
        s.HasDisability = true;
        s.DisabilityTypes = "Hearing Impairment,Cancer";
        s.HomeAddress = new PsgcAddress { CityMunicipalityCode = "1380600000", BarangayCode = "1380601001" };

        var dto = StudentMapping.ToDto(s);

        Assert.Equal("2026-00009", dto.StudentNumber);
        Assert.Equal(new[] { "Hearing Impairment", "Cancer" }, dto.Profile!.DisabilityTypes);
        Assert.Equal("1380601001", dto.Profile.HomeAddress!.BarangayCode);
    }

    [Fact]
    public void Apply_ClearsFollowUpFields_WhenTheirFlagIsOff()
    {
        var target = S("2026-00010", "Dee");
        LearnerProfileMapping.Apply(new LearnerProfileDto(
            IsIndigenousPeople: false, IpCommunity: "Aeta",
            Is4PsBeneficiary: false, FourPsHouseholdId: "HH-1",
            HasDisability: false, DisabilityTypes: new() { "Cancer" },
            Religion: "  Catholic  "), target);

        Assert.Null(target.IpCommunity);
        Assert.Null(target.FourPsHouseholdId);
        Assert.Null(target.DisabilityTypes);
        Assert.Equal("Catholic", target.Religion);
    }

    [Fact]
    public void FillBlanks_NeverOverwritesExistingValues()
    {
        var target = S("2026-00011", "Eve");
        target.Religion = "Catholic";
        var source = S("2026-00012", "Eve");
        source.Religion = "INC";
        source.MotherTongue = "Ilocano";
        source.IsIndigenousPeople = true;
        source.IpCommunity = "Igorot";
        source.HomeAddress = new PsgcAddress { RegionCode = "0100000000", CityMunicipalityCode = "0102800000", BarangayCode = "0102801001" };

        LearnerProfileMapping.FillBlanks(source, target);

        Assert.Equal("Catholic", target.Religion);
        Assert.Equal("Ilocano", target.MotherTongue);
        Assert.True(target.IsIndigenousPeople);
        Assert.Equal("Igorot", target.IpCommunity);
        Assert.Equal("0102801001", target.HomeAddress.BarangayCode);
    }

    [Theory]
    [InlineData("13000000", "Region code must be a 10-digit PSGC code.")]
    [InlineData("١٣٠٠٠٠٠٠٠٠", "Region code must be a 10-digit PSGC code.")] // non-ASCII digits
    public void PsgcValidator_RejectsBadRegionCodes(string code, string message)
    {
        var result = new PsgcAddressDtoValidator().Validate(new PsgcAddressDto(RegionCode: code, RegionName: "X"));
        Assert.Contains(result.Errors, e => e.ErrorMessage == message);
    }

    [Fact]
    public void PsgcValidator_BarangayRequiresCity()
    {
        var result = new PsgcAddressDtoValidator().Validate(new PsgcAddressDto(
            RegionCode: "1300000000", RegionName: "NCR", BarangayCode: "1380601001", BarangayName: "Barangay 1"));
        Assert.Contains(result.Errors, e => e.PropertyName == "CityMunicipalityCode");
    }

    [Fact]
    public void ProfileValidator_EnforcesFollowUpsAndKnownDisabilityTypes()
    {
        var result = new LearnerProfileDtoValidator().Validate(new LearnerProfileDto(
            IsIndigenousPeople: true, Is4PsBeneficiary: true, HasDisability: true, DisabilityTypes: new() { "Flu" }));

        Assert.Contains(result.Errors, e => e.PropertyName == "IpCommunity");
        Assert.Contains(result.Errors, e => e.PropertyName == "FourPsHouseholdId");
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("'Flu' is not a recognized disability type"));
    }

    [Fact]
    public void ProfileValidator_RequiresAtLeastOneDisabilityType()
    {
        var result = new LearnerProfileDtoValidator().Validate(new LearnerProfileDto(HasDisability: true));
        Assert.Contains(result.Errors, e => e.PropertyName == "DisabilityTypes");
    }

    [Theory]
    [InlineData("LearnerType", "Graduate")]
    [InlineData("LastSchoolYearCompleted", "2025/2026")]
    [InlineData("LastSchoolId", "30512")]
    [InlineData("LastGradeLevelCompleted", "Grade 13")]
    public void LearnerInfoValidator_RejectsMalformedValues(string property, string value)
    {
        var dto = property switch
        {
            "LearnerType" => new EnrollmentLearnerInfoDto(LearnerType: value),
            "LastSchoolYearCompleted" => new EnrollmentLearnerInfoDto(LastSchoolYearCompleted: value),
            "LastSchoolId" => new EnrollmentLearnerInfoDto(LastSchoolId: value),
            _ => new EnrollmentLearnerInfoDto(LastGradeLevelCompleted: value),
        };

        var result = new EnrollmentLearnerInfoDtoValidator().Validate(dto);
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void LearnerInfoValidator_AcceptsBlankStrings()
    {
        var result = new EnrollmentLearnerInfoDtoValidator().Validate(
            new EnrollmentLearnerInfoDto(LearnerType: "", LastSchoolId: " ", LastSchoolYearCompleted: ""));
        Assert.True(result.IsValid);
    }
}
