using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments;

/// <summary>
/// Picks "the" enrollment for self-service flows (payments, plan selection) deterministically:
/// the newest enrollment in the active school year, falling back to the newest overall.
/// A returning student has one enrollment per year, so an unordered FirstOrDefault would
/// return an arbitrary one.
/// </summary>
public static class EnrollmentSelector
{
    public static async Task<Enrollment?> PickCurrentAsync(
        IApplicationDbContext context,
        IQueryable<Enrollment> candidates,
        CancellationToken ct)
    {
        candidates = candidates.Where(e => e.Status != Domain.Enums.EnrollmentStatus.Cancelled);

        var activeSchoolYear = await context.SchoolYears
            .Where(sy => sy.IsActive)
            .Select(sy => sy.Name)
            .FirstOrDefaultAsync(ct);

        if (activeSchoolYear != null)
        {
            var current = await candidates
                .Where(e => e.SchoolYear == activeSchoolYear)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (current != null) return current;
        }

        return await candidates
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
