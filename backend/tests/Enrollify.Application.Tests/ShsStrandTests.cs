using Enrollify.Application.Features.ShsStrands;
using Enrollify.Application.Features.ShsStrands.Commands;
using Enrollify.Application.Features.ShsStrands.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Enrollify.Application.Tests;

public class ShsStrandTests
{
    private static async Task<ApplicationDbContext> WithStemAsync(bool active = true)
    {
        var ctx = TestDb.Create();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "STEM", IsActive = active, DisplayOrder = 10 });
        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task Ensure_SeedsDefaultsOnce()
    {
        var ctx = TestDb.Create();
        Assert.Equal(10, await DefaultShsStrands.EnsureForTenantAsync(ctx, TestDb.TenantId));
        await ctx.SaveChangesAsync();
        Assert.Equal(0, await DefaultShsStrands.EnsureForTenantAsync(ctx, TestDb.TenantId));
        await ctx.SaveChangesAsync();

        Assert.Equal(10, await ctx.ShsStrands.CountAsync());
        Assert.True(await ctx.ShsStrands.AnyAsync(s => s.Track == "Academic" && s.Strand == "STEM"));
    }

    [Fact]
    public async Task Ensure_DoesNotReseed_WhenEveryStrandWasDeactivated()
    {
        var ctx = await WithStemAsync(active: false);
        Assert.Equal(0, await DefaultShsStrands.EnsureForTenantAsync(ctx, TestDb.TenantId));
    }

    [Fact]
    public async Task Create_RefusesCaseInsensitiveDuplicate()
    {
        var ctx = await WithStemAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CreateShsStrandCommandHandler(ctx).Handle(new CreateShsStrandCommand(" academic ", "stem", 20), default));
    }

    [Fact]
    public async Task Update_RefusesDuplicateOfAnotherRow_ButAllowsSelf()
    {
        var ctx = await WithStemAsync();
        var abm = new ShsStrand { Track = "Academic", Strand = "ABM", IsActive = true };
        ctx.ShsStrands.Add(abm);
        await ctx.SaveChangesAsync();
        var handler = new UpdateShsStrandCommandHandler(ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new UpdateShsStrandCommand(abm.Id, "Academic", "STEM", true, 0), default));
        var dto = await handler.Handle(new UpdateShsStrandCommand(abm.Id, "Academic", "ABM", false, 5), default);
        Assert.False(dto.IsActive);
    }

    [Fact]
    public async Task Resolve_BlankPair_ReturnsNulls()
    {
        var ctx = await WithStemAsync();
        var (track, strand) = await ShsStrandRules.ResolveAsync(ctx, "Grade 11", " ", null, default);
        Assert.Null(track);
        Assert.Null(strand);
    }

    [Fact]
    public async Task Resolve_NormalizesToCatalogCasing()
    {
        var ctx = await WithStemAsync();
        Assert.Equal(("Academic", "STEM"), await ShsStrandRules.ResolveAsync(ctx, "Grade 12", "academic", " stem ", default));
    }

    [Theory]
    [InlineData("Grade 8", "Academic", "STEM", "only applies to Grade 11 and Grade 12")]
    [InlineData("Grade 11", "Academic", null, "Choose both a track and a strand")]
    [InlineData("Grade 11", "TVL", "ICT", "not a strand this school currently offers")]
    public async Task Resolve_RefusesInvalidPairs(string grade, string? track, string? strand, string message)
    {
        var ctx = await WithStemAsync();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ShsStrandRules.ResolveAsync(ctx, grade, track, strand, default));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public async Task Resolve_InactiveStrand_IsRefused()
    {
        var ctx = await WithStemAsync(active: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ShsStrandRules.ResolveAsync(ctx, "Grade 11", "Academic", "STEM", default));
    }

    [Fact]
    public async Task Resolve_ExplicitTenant_IgnoresTheAmbientTenant()
    {
        var ctx = TestDb.Create();
        var otherTenant = Guid.NewGuid();
        ctx.ShsStrands.Add(new ShsStrand { TenantId = otherTenant, Track = "Academic", Strand = "HUMSS", IsActive = true });
        await ctx.SaveChangesAsync();

        Assert.Equal(("Academic", "HUMSS"),
            await ShsStrandRules.ResolveAsync(ctx, "Grade 11", "Academic", "HUMSS", default, otherTenant));
    }

    [Fact]
    public async Task IsStrandRequired_OnlyForShsGradesWithActiveStrands()
    {
        var active = await WithStemAsync();
        Assert.True(await ShsStrandRules.IsStrandRequiredAsync(active, "Grade 11", default));
        Assert.False(await ShsStrandRules.IsStrandRequiredAsync(active, "Grade 10", default));

        var switchedOff = await WithStemAsync(active: false);
        Assert.False(await ShsStrandRules.IsStrandRequiredAsync(switchedOff, "Grade 11", default));
    }

    [Fact]
    public async Task PublicQuery_ReturnsOnlyActiveStrandsOfThatTenant()
    {
        var ctx = await WithStemAsync();
        ctx.ShsStrands.Add(new ShsStrand { Track = "Academic", Strand = "GAS", IsActive = false });
        ctx.ShsStrands.Add(new ShsStrand { TenantId = Guid.NewGuid(), Track = "TVL", Strand = "ICT", IsActive = true });
        await ctx.SaveChangesAsync();

        var list = await new GetShsStrandsByTenantQueryHandler(ctx).Handle(new GetShsStrandsByTenantQuery(TestDb.TenantId), default);

        Assert.Equal("STEM", Assert.Single(list).Strand);
    }
}
