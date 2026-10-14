using System.Globalization;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments;

/// <summary>
/// The single place a new enrollment's requirement checklist is created (approval, staff
/// create, parent/student requests, bulk re-enroll). Active templates matching the grade
/// (GradeLevel == null or == grade) become requirements; for a KeepOnFile template the
/// learner's most recent VERIFIED copy of that document — from ANY earlier enrollment,
/// cancelled ones included — is carried over already verified, pointing at the same file.
/// Submitted-unverified and rejected copies never carry. Callers save.
///
/// A FileDocument can therefore be referenced by several enrollments' requirements: any
/// future blob cleanup must check every reference before deleting (sub-project 7).
/// </summary>
public static class EnrollmentRequirements
{
    public static Task AddForAsync(IApplicationDbContext context, Enrollment enrollment, CancellationToken ct, Guid? tenantId = null)
        => AddForManyAsync(context, new[] { enrollment }, ct, tenantId);

    public static async Task AddForManyAsync(
        IApplicationDbContext context, IReadOnlyCollection<Enrollment> enrollments, CancellationToken ct, Guid? tenantId = null)
    {
        if (enrollments.Count == 0) return;

        var templates = await context.RequirementTemplates
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.DocumentName)
            .ToListAsync(ct);

        var keepOnFileNames = templates.Where(t => t.KeepOnFile).Select(t => Normalize(t.DocumentName)).ToHashSet();
        var onFile = new Dictionary<(Guid StudentId, string Doc), OnFileCopy>();

        if (keepOnFileNames.Count > 0)
        {
            var studentIds = enrollments.Select(e => e.StudentId).Distinct().ToList();
            // One query for every student: their verified requirements (filtered by name in memory,
            // because the name match is trim/case-insensitive).
            var verified = await context.EnrollmentRequirements
                .Where(r => r.IsVerified && studentIds.Contains(r.Enrollment.StudentId))
                .Select(r => new OnFileCopy(
                    r.Enrollment.StudentId, r.Enrollment.SchoolYear, r.DocumentName,
                    r.FileName, r.Notes, r.VerifiedBy, r.VerifiedAt, r.CreatedAt))
                .ToListAsync(ct);

            foreach (var group in verified
                         .Where(r => keepOnFileNames.Contains(Normalize(r.DocumentName)))
                         .GroupBy(r => (r.StudentId, Normalize(r.DocumentName))))
            {
                onFile[group.Key] = group.OrderByDescending(r => r.VerifiedAt ?? r.CreatedAt).First();
            }
        }

        foreach (var enrollment in enrollments)
        {
            foreach (var template in templates.Where(t => t.GradeLevel == null || t.GradeLevel == enrollment.GradeLevel))
            {
                var requirement = new EnrollmentRequirement
                {
                    EnrollmentId = enrollment.Id,
                    DocumentName = template.DocumentName
                };
                if (tenantId != null) requirement.TenantId = tenantId.Value;

                if (template.KeepOnFile && onFile.TryGetValue((enrollment.StudentId, Normalize(template.DocumentName)), out var prior))
                {
                    requirement.IsSubmitted = true;
                    requirement.IsVerified = true;
                    requirement.FileName = prior.FileName;
                    requirement.Notes = prior.Notes;
                    requirement.VerifiedBy = prior.VerifiedBy;
                    requirement.VerifiedAt = prior.VerifiedAt;
                    var when = prior.VerifiedAt?.ToString("MMM d, yyyy", CultureInfo.InvariantCulture) ?? "earlier";
                    requirement.ReviewNotes = $"On file — verified {when} (SY {prior.SchoolYear})";
                }

                context.EnrollmentRequirements.Add(requirement);
            }
        }
    }

    private static string Normalize(string name) => name.Trim().ToLowerInvariant();

    private record OnFileCopy(
        Guid StudentId, string SchoolYear, string DocumentName,
        string? FileName, string? Notes, string? VerifiedBy, DateTime? VerifiedAt, DateTime CreatedAt);
}
