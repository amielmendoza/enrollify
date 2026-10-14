using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands;

/// <summary>
/// The default SHS offerings (current DepEd tracks/strands). Seeded only for a tenant with NO
/// strand rows at all — strands are deactivated, never deleted, so an empty catalog means
/// "never seeded" and a school that switched SHS off is left alone. Caller saves.
/// </summary>
public static class DefaultShsStrands
{
    private static readonly (string Track, string Strand)[] Defaults =
    {
        ("Academic", "STEM"), ("Academic", "ABM"), ("Academic", "HUMSS"), ("Academic", "GAS"),
        ("TVL", "ICT"), ("TVL", "HE"), ("TVL", "IA"), ("TVL", "AFA"),
        ("Sports", "Sports"), ("Arts and Design", "Arts and Design"),
    };

    public static async Task<int> EnsureForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var any = await context.ShsStrands.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenantId, ct);
        if (any) return 0;

        for (var i = 0; i < Defaults.Length; i++)
        {
            context.ShsStrands.Add(new ShsStrand
            {
                TenantId = tenantId,
                Track = Defaults[i].Track,
                Strand = Defaults[i].Strand,
                IsActive = true,
                DisplayOrder = (i + 1) * 10
            });
        }
        return Defaults.Length;
    }
}
