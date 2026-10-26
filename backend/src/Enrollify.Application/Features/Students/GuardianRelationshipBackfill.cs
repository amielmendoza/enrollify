using Enrollify.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students;

/// <summary>
/// Idempotent per-tenant backfill: a Student with no GuardianRelationship takes it from their most
/// recently updated Approved application that has a non-blank value. Students that already have a
/// value are never touched, so a second run changes nothing. Mirrors the SQL in the AddDepEdReports
/// migration (which does this once for existing rows); this copy covers InMemory and any row the
/// SQL left alone. Caller saves.
/// </summary>
public static class GuardianRelationshipBackfill
{
    public static async Task<int> RunForTenantAsync(IApplicationDbContext context, Guid tenantId, CancellationToken ct = default)
    {
        var students = await context.Students.IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && s.GuardianRelationship == null)
            .ToListAsync(ct);
        if (students.Count == 0) return 0;

        var apps = await context.AdmissionApplications.IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId && a.Status == "Approved"
                        && a.StudentId != null && a.GuardianRelationship != null)
            .Select(a => new { StudentId = a.StudentId!.Value, a.GuardianRelationship, a.UpdatedAt, a.CreatedAt })
            .ToListAsync(ct);

        var latest = apps
            .Where(a => !string.IsNullOrWhiteSpace(a.GuardianRelationship))
            .GroupBy(a => a.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(a => a.UpdatedAt).ThenByDescending(a => a.CreatedAt).First().GuardianRelationship!.Trim());

        var changes = 0;
        foreach (var student in students)
        {
            if (!latest.TryGetValue(student.Id, out var relationship)) continue;
            student.GuardianRelationship = relationship;
            changes++;
        }
        return changes;
    }
}
