using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.Features.Students.Commands;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class FamilyProfileUpdateTests
{
    private static readonly PsgcAddressDto QcBarangay = new(
        "1300000000", "NCR", null, null, "1381300000", "Quezon City", "1381300001", "Alicia", "1105");

    [Fact]
    public async Task Parent_UpdatesAddressAndParentContacts_IdentityFieldsUntouched()
    {
        var ctx = TestDb.Create();
        var parentId = Guid.NewGuid();
        var s = new Student
        {
            StudentNumber = "2026-00001", FirstName = "Ana", LastName = "Cruz", Address = "old",
            ParentUserId = parentId, PsaBirthCertificateNumber = "PSA-1", Religion = "Catholic"
        };
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateChildProfileCommandHandler(ctx).Handle(new UpdateChildProfileCommand(
            s.Id, parentId, null, null, "12 Mabini St", null, null,
            HomeAddress: QcBarangay, FatherContactNumber: "0917-111-1111", MotherContactNumber: "0917-222-2222"), default);

        var reloaded = await ctx.Students.SingleAsync();
        Assert.Equal("1381300001", reloaded.HomeAddress.BarangayCode);
        Assert.Equal("12 Mabini St", reloaded.Address);
        Assert.Equal("0917-111-1111", reloaded.FatherContactNumber);
        Assert.Equal("PSA-1", reloaded.PsaBirthCertificateNumber);
        Assert.Equal("Catholic", reloaded.Religion);
    }

    [Fact]
    public async Task OmittedAddress_LeavesExistingAddressAlone()
    {
        var ctx = TestDb.Create();
        var userId = Guid.NewGuid();
        var s = new Student { StudentNumber = "2026-00002", FirstName = "Ben", LastName = "Uy", Address = "", UserId = userId };
        s.HomeAddress.BarangayCode = "1381300001";
        s.HomeAddress.CityMunicipalityCode = "1381300000";
        ctx.Students.Add(s);
        await ctx.SaveChangesAsync();

        await new UpdateMyProfileCommandHandler(ctx).Handle(new UpdateMyProfileCommand(
            userId, "0917-000-0000", null, null, null, null), default);

        Assert.Equal("1381300001", (await ctx.Students.SingleAsync()).HomeAddress.BarangayCode);
    }

    [Fact]
    public void Validator_RejectsMalformedPsgcCode()
    {
        var result = new UpdateChildProfileCommandValidator().Validate(new UpdateChildProfileCommand(
            Guid.NewGuid(), Guid.NewGuid(), null, null, null, null, null,
            HomeAddress: new PsgcAddressDto(RegionCode: "13", RegionName: "NCR")));
        Assert.False(result.IsValid);
    }
}
