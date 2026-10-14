using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.ShsStrands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments;

/// <summary>
/// Shared plan for a family-initiated enrollment request (parent for a child, student for
/// themselves). Grade: promote from the latest non-cancelled enrollment in another year; else
/// the approved application's grade; else Grade 7. Track/strand: the requested pair (validated)
/// when given; else carried from a previous SHS enrollment (11 → 12); else the approved
/// application's pair when the grade is SHS; else empty — the registrar sets it before assessment.
/// Carried strands are kept even if since deactivated (continuing learners finish their program).
/// Learner type: Continuing when there's a previous enrollment, else the application's, else New.
/// </summary>
public static class EnrollmentRequestPlanner
{
    public record Plan(string GradeLevel, string LearnerType, string? Track, string? Strand);

    public static async Task<Plan> PlanAsync(
        IApplicationDbContext context, Guid studentId, string schoolYear,
        string? requestedTrack, string? requestedStrand, CancellationToken ct)
    {
        var previous = await context.Enrollments
            .Where(e => e.StudentId == studentId && e.SchoolYear != schoolYear && e.Status != EnrollmentStatus.Cancelled)
            .OrderByDescending(e => e.SchoolYear).ThenByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        AdmissionApplication? application = null;
        string grade;
        if (previous != null)
        {
            grade = GradeLevels.Promote(previous.GradeLevel);
        }
        else
        {
            application = await context.AdmissionApplications
                .FirstOrDefaultAsync(a => a.StudentId == studentId && a.Status == "Approved", ct);
            grade = application?.GradeLevel ?? "Grade 7";
        }

        var (track, strand) = await ShsStrandRules.ResolveAsync(context, grade, requestedTrack, requestedStrand, ct);
        if (strand == null && DepEdLists.IsShsGrade(grade))
        {
            if (previous != null && DepEdLists.IsShsGrade(previous.GradeLevel))
                (track, strand) = (previous.Track, previous.Strand);
            else if (application != null)
                (track, strand) = (application.Track, application.Strand);
        }

        var learnerType = previous != null ? "Continuing" : string.IsNullOrWhiteSpace(application?.LearnerType) ? "New" : application!.LearnerType!;
        return new Plan(grade, learnerType, track, strand);
    }
}
