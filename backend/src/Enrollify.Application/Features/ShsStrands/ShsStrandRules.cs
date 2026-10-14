using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands;

/// <summary>
/// The one definition of a valid enrollment track/strand. Tenant scoping comes from the normal
/// query filter, or from an explicit <c>tenantId</c> for the slug-based public apply flow.
/// </summary>
public static class ShsStrandRules
{
    /// <returns>The pair in catalog casing, or (null, null) when both are blank.</returns>
    /// <exception cref="InvalidOperationException">Strand on a non-SHS grade, half a pair, or a pair the school doesn't currently offer.</exception>
    public static async Task<(string? Track, string? Strand)> ResolveAsync(
        IApplicationDbContext context, string gradeLevel, string? track, string? strand, CancellationToken ct, Guid? tenantId = null)
    {
        var t = string.IsNullOrWhiteSpace(track) ? null : track.Trim();
        var s = string.IsNullOrWhiteSpace(strand) ? null : strand.Trim();
        if (t == null && s == null) return (null, null);

        if (!DepEdLists.IsShsGrade(gradeLevel))
            throw new InvalidOperationException($"Track/strand only applies to Grade 11 and Grade 12 (this is {gradeLevel}).");
        if (t == null || s == null)
            throw new InvalidOperationException("Choose both a track and a strand.");

        var tl = t.ToLower();
        var sl = s.ToLower();
        var match = await Catalog(context, tenantId)
            .FirstOrDefaultAsync(x => x.IsActive && x.Track.ToLower() == tl && x.Strand.ToLower() == sl, ct)
            ?? throw new InvalidOperationException($"'{t} – {s}' is not a strand this school currently offers.");
        return (match.Track, match.Strand);
    }

    /// <summary>True when the grade is SHS and the school offers at least one active strand.</summary>
    public static async Task<bool> IsStrandRequiredAsync(IApplicationDbContext context, string gradeLevel, CancellationToken ct, Guid? tenantId = null) =>
        DepEdLists.IsShsGrade(gradeLevel) && await Catalog(context, tenantId).AnyAsync(x => x.IsActive, ct);

    private static IQueryable<ShsStrand> Catalog(IApplicationDbContext context, Guid? tenantId) =>
        tenantId == null
            ? context.ShsStrands
            : context.ShsStrands.IgnoreQueryFilters().Where(x => x.TenantId == tenantId.Value);
}
