using Enrollify.Application.Features.SchoolProfile;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class DefaultDepEdProfileTests
{
    [Fact]
    public async Task Sets_placeholder_profile_on_an_empty_tenant_once()
    {
        var ctx = TestDb.Create();
        ctx.Tenants.Add(new Tenant { Id = TestDb.TenantId, Name = "Demo", Subdomain = "demo" });
        await ctx.SaveChangesAsync();

        Assert.Equal(1, await DefaultDepEdProfile.EnsureForTenantAsync(ctx, TestDb.TenantId, default));
        await ctx.SaveChangesAsync();
        Assert.Equal(0, await DefaultDepEdProfile.EnsureForTenantAsync(ctx, TestDb.TenantId, default));

        var t = await ctx.Tenants.SingleAsync();
        Assert.Equal("000000", t.DepEdSchoolId);
        Assert.Equal("1300000000", t.DepEdRegionCode);
        Assert.Equal("National Capital Region (NCR)", t.DepEdRegionName);
        Assert.Equal("Demo Division", t.DepEdDivision);
        Assert.Equal("Demo District", t.DepEdDistrict);
        Assert.Equal("Demo School Head", t.SchoolHeadName);
    }

    [Fact]
    public async Task Leaves_a_tenant_with_any_real_value_alone()
    {
        var ctx = TestDb.Create();
        ctx.Tenants.Add(new Tenant { Id = TestDb.TenantId, Name = "Real", Subdomain = "real", DepEdSchoolId = "305123" });
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await DefaultDepEdProfile.EnsureForTenantAsync(ctx, TestDb.TenantId, default));

        var t = await ctx.Tenants.SingleAsync();
        Assert.Equal("305123", t.DepEdSchoolId);
        Assert.Null(t.DepEdDivision);
    }
}
