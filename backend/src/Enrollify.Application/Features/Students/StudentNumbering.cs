using Enrollify.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students;

/// <summary>
/// Enrollify student numbers: "YYYY-NNNNN" — the Manila calendar year plus a per-tenant,
/// per-year 5-digit sequence. Deliberately NOT LRN-shaped (LRNs are DepEd-issued, 12 digits).
/// Two concurrent requests can compute the same next number; the (TenantId, StudentNumber)
/// unique index rejects the loser, so callers inserting a fresh number retry a couple of times,
/// regenerating between attempts.
/// </summary>
public static class StudentNumbering
{
    private static readonly TimeSpan ManilaOffset = TimeSpan.FromHours(8); // PH has no DST

    public static int ManilaYear(DateTime utcNow) => (utcNow + ManilaOffset).Year;

    public static async Task<string> NextStudentNumberAsync(IApplicationDbContext context, DateTime utcNow, CancellationToken ct)
    {
        var prefix = $"{ManilaYear(utcNow)}-";
        // Fixed width within a year, so lexicographic max == numeric max.
        var max = await context.Students
            .Where(s => s.StudentNumber.StartsWith(prefix) && s.StudentNumber.Length == prefix.Length + 5)
            .OrderByDescending(s => s.StudentNumber)
            .Select(s => s.StudentNumber)
            .FirstOrDefaultAsync(ct);

        var next = max != null && int.TryParse(max.AsSpan(prefix.Length), out var seq) ? seq + 1 : 1;
        return $"{prefix}{next:D5}";
    }
}
