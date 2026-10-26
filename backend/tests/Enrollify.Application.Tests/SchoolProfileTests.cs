using Enrollify.Application.Features.SchoolProfile.Commands;
using Enrollify.Application.Features.SchoolProfile.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Infrastructure.Persistence;
using Xunit;

namespace Enrollify.Application.Tests;

public class SchoolProfileTests
{
    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var ctx = TestDb.Create();
        // Tenant is not a TenantEntity, so it is not auto-created or filtered; other tenants
        // must never be returned or touched.
        ctx.Tenants.Add(new Tenant { Id = TestDb.TenantId, Name = "Mountain State High School", Subdomain = "mshs" });
        ctx.Tenants.Add(new Tenant { Id = Guid.NewGuid(), Name = "Other School", Subdomain = "other" });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static UpdateSchoolProfileCommand Cmd(
        string? schoolId = null, string? regionCode = null, string? regionName = null,
        string? division = null, string? district = null, string? head = null) =>
        new(TestDb.TenantId, schoolId, regionCode, regionName, division, district, head, "Admin Ana");

    private static bool IsValid(UpdateSchoolProfileCommand cmd) =>
        new UpdateSchoolProfileCommandValidator().Validate(cmd).IsValid;

    // ---- query ----

    [Fact]
    public async Task Query_ReturnsTenantNameAsSchoolName_AndProfileFields()
    {
        var ctx = await SeedAsync();
        var tenant = ctx.Tenants.Single(t => t.Id == TestDb.TenantId);
        tenant.DepEdSchoolId = "301234";
        tenant.DepEdRegionCode = "0100000000";
        tenant.DepEdRegionName = "Region I";
        tenant.DepEdDivision = "Baguio City";
        tenant.DepEdDistrict = "District 1";
        tenant.SchoolHeadName = "Dr. Head";
        await ctx.SaveChangesAsync();

        var dto = await new GetSchoolProfileQueryHandler(ctx).Handle(new GetSchoolProfileQuery(TestDb.TenantId), default);

        Assert.Equal("Mountain State High School", dto.SchoolName);
        Assert.Equal("301234", dto.DepEdSchoolId);
        Assert.Equal("0100000000", dto.DepEdRegionCode);
        Assert.Equal("Region I", dto.DepEdRegionName);
        Assert.Equal("Baguio City", dto.DepEdDivision);
        Assert.Equal("District 1", dto.DepEdDistrict);
        Assert.Equal("Dr. Head", dto.SchoolHeadName);
    }

    [Fact]
    public async Task Query_UnfilledProfile_ReturnsNullFields()
    {
        var ctx = await SeedAsync();

        var dto = await new GetSchoolProfileQueryHandler(ctx).Handle(new GetSchoolProfileQuery(TestDb.TenantId), default);

        Assert.Equal("Mountain State High School", dto.SchoolName);
        Assert.Null(dto.DepEdSchoolId);
        Assert.Null(dto.DepEdRegionCode);
        Assert.Null(dto.SchoolHeadName);
    }

    // ---- validation ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("301234")]
    [InlineData(" 301234 ")]
    public void SchoolId_NullBlankOrSixDigits_IsValid(string? id) => Assert.True(IsValid(Cmd(schoolId: id)));

    [Theory]
    [InlineData("30123")]
    [InlineData("3012345")]
    [InlineData("30123A")]
    [InlineData("30 234")]
    public void SchoolId_NotSixDigits_IsRejected(string id) => Assert.False(IsValid(Cmd(schoolId: id)));

    [Theory]
    [InlineData("0100000000")]
    [InlineData("1300000000")]
    [InlineData("0700000000")]
    public void RegionCode_TwoDigitsPlusEightZeros_IsValid(string code) =>
        Assert.True(IsValid(Cmd(regionCode: code, regionName: "Some Region")));

    [Theory]
    [InlineData("01")]
    [InlineData("010000000")]       // 9 chars
    [InlineData("01000000000")]     // 11 chars
    [InlineData("0100000001")]      // non-zero tail
    [InlineData("A100000000")]
    public void RegionCode_Malformed_IsRejected(string code) =>
        Assert.False(IsValid(Cmd(regionCode: code, regionName: "Some Region")));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RegionName_RequiredWhenCodeSet(string? name) =>
        Assert.False(IsValid(Cmd(regionCode: "0100000000", regionName: name)));

    [Fact]
    public void RegionName_NotRequiredWhenCodeBlank() =>
        Assert.True(IsValid(Cmd(regionCode: "  ", regionName: null)));

    [Fact]
    public void RegionName_WithoutCode_IsAllowed() =>
        Assert.True(IsValid(Cmd(regionName: "Region I")));

    [Fact]
    public void DivisionDistrictHead_Exactly150_IsValid()
    {
        var s = new string('x', 150);
        Assert.True(IsValid(Cmd(division: s, district: s, head: s)));
    }

    [Fact]
    public void Division_Over150_IsRejected() => Assert.False(IsValid(Cmd(division: new string('x', 151))));

    [Fact]
    public void District_Over150_IsRejected() => Assert.False(IsValid(Cmd(district: new string('x', 151))));

    [Fact]
    public void SchoolHead_Over150_IsRejected() => Assert.False(IsValid(Cmd(head: new string('x', 151))));

    [Fact]
    public void RegionName_Over150_IsRejected() =>
        Assert.False(IsValid(Cmd(regionCode: "0100000000", regionName: new string('x', 151))));

    [Fact]
    public void WhitespacePaddedValue_IsMeasuredTrimmed()
    {
        // 150 real chars plus padding: trimmed length is 150, so it passes.
        var s = "  " + new string('x', 150) + "  ";
        Assert.True(IsValid(Cmd(division: s)));
    }

    // ---- update handler ----

    [Fact]
    public async Task Update_StoresFields_AndReturnsDto()
    {
        var ctx = await SeedAsync();

        var dto = await new UpdateSchoolProfileCommandHandler(ctx).Handle(
            Cmd("301234", "0100000000", "Region I", "Baguio City", "District 1", "Dr. Head"), default);

        Assert.Equal("Mountain State High School", dto.SchoolName);
        Assert.Equal("301234", dto.DepEdSchoolId);
        var stored = ctx.Tenants.Single(t => t.Id == TestDb.TenantId);
        Assert.Equal("301234", stored.DepEdSchoolId);
        Assert.Equal("0100000000", stored.DepEdRegionCode);
        Assert.Equal("Region I", stored.DepEdRegionName);
        Assert.Equal("Baguio City", stored.DepEdDivision);
        Assert.Equal("District 1", stored.DepEdDistrict);
        Assert.Equal("Dr. Head", stored.SchoolHeadName);
    }

    [Fact]
    public async Task Update_BlankStrings_AreStoredAsNull()
    {
        var ctx = await SeedAsync();
        var tenant = ctx.Tenants.Single(t => t.Id == TestDb.TenantId);
        tenant.DepEdSchoolId = "301234";
        tenant.DepEdDivision = "Old Division";
        tenant.SchoolHeadName = "Old Head";
        await ctx.SaveChangesAsync();

        var dto = await new UpdateSchoolProfileCommandHandler(ctx).Handle(
            Cmd(schoolId: "", regionCode: " ", regionName: "\t", division: "   ", district: "", head: " "), default);

        Assert.Null(dto.DepEdSchoolId);
        Assert.Null(dto.DepEdRegionCode);
        Assert.Null(dto.DepEdRegionName);
        Assert.Null(dto.DepEdDivision);
        Assert.Null(dto.DepEdDistrict);
        Assert.Null(dto.SchoolHeadName);
        var stored = ctx.Tenants.Single(t => t.Id == TestDb.TenantId);
        Assert.Null(stored.DepEdSchoolId);
        Assert.Null(stored.DepEdDivision);
        Assert.Null(stored.SchoolHeadName);
    }

    [Fact]
    public async Task Update_TrimsValues()
    {
        var ctx = await SeedAsync();

        var dto = await new UpdateSchoolProfileCommandHandler(ctx).Handle(
            Cmd(schoolId: " 301234 ", division: "  Baguio City ", head: " Dr. Head  "), default);

        Assert.Equal("301234", dto.DepEdSchoolId);
        Assert.Equal("Baguio City", dto.DepEdDivision);
        Assert.Equal("Dr. Head", dto.SchoolHeadName);
    }

    [Fact]
    public async Task Update_DoesNotChangeName_OrTouchOtherTenants()
    {
        var ctx = await SeedAsync();

        await new UpdateSchoolProfileCommandHandler(ctx).Handle(Cmd(schoolId: "301234", head: "Dr. Head"), default);

        Assert.Equal("Mountain State High School", ctx.Tenants.Single(t => t.Id == TestDb.TenantId).Name);
        var other = ctx.Tenants.Single(t => t.Id != TestDb.TenantId);
        Assert.Equal("Other School", other.Name);
        Assert.Null(other.DepEdSchoolId);
        Assert.Null(other.SchoolHeadName);
    }
}
