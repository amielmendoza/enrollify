using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Batch re-enrollment for a new school year: every student who finished FromSchoolYear
/// (status Enrolled) gets a Draft enrollment in ToSchoolYear at the next grade up.
/// Grade 12 finishers graduate (skipped); students who already have a non-cancelled
/// ToSchoolYear enrollment are skipped. All-or-nothing via a single transaction.
/// </summary>
public record BulkReenrollCommand(string FromSchoolYear, string ToSchoolYear) : IRequest<BulkReenrollResultDto>;

public record BulkReenrollResultDto(int Created, int SkippedExisting, int SkippedGraduates);

public class BulkReenrollCommandValidator : AbstractValidator<BulkReenrollCommand>
{
    public BulkReenrollCommandValidator()
    {
        RuleFor(x => x.FromSchoolYear).NotEmpty();
        RuleFor(x => x.ToSchoolYear).NotEmpty()
            .Must((cmd, to) => to != cmd.FromSchoolYear)
            .WithMessage("Target school year must differ from the source school year.");
    }
}

public class BulkReenrollCommandHandler : IRequestHandler<BulkReenrollCommand, BulkReenrollResultDto>
{
    private readonly IApplicationDbContext _context;

    public BulkReenrollCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<BulkReenrollResultDto> Handle(BulkReenrollCommand request, CancellationToken cancellationToken)
    {
        // Guard against typo'd targets from raw API calls: a whole cohort must never be
        // enrolled into a school year that doesn't exist for this tenant.
        var targetYearExists = await _context.SchoolYears
            .AnyAsync(sy => sy.Name == request.ToSchoolYear, cancellationToken);
        if (!targetYearExists)
            throw new InvalidOperationException(
                $"School year '{request.ToSchoolYear}' does not exist. Create it first under Settings → School Years.");

        int created = 0, skippedExisting = 0, skippedGraduates = 0;

        await _context.ExecuteInTransactionAsync(async ct =>
        {
            created = 0; skippedExisting = 0; skippedGraduates = 0;

            // Students who completed the source year: one row per student (Enrolled is terminal
            // per year, so duplicates would be data noise — take the most recent).
            var finished = await _context.Enrollments
                .Where(e => e.SchoolYear == request.FromSchoolYear && e.Status == EnrollmentStatus.Enrolled)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(ct);
            var perStudent = finished
                .GroupBy(e => e.StudentId)
                .Select(g => g.First())
                .ToList();

            var alreadyInTarget = (await _context.Enrollments
                    .Where(e => e.SchoolYear == request.ToSchoolYear && e.Status != EnrollmentStatus.Cancelled)
                    .Select(e => e.StudentId)
                    .ToListAsync(ct))
                .ToHashSet();

            var templates = await _context.RequirementTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayOrder).ThenBy(t => t.DocumentName)
                .ToListAsync(ct);

            foreach (var source in perStudent)
            {
                if (source.GradeLevel == "Grade 12")
                {
                    skippedGraduates++;
                    continue;
                }

                if (alreadyInTarget.Contains(source.StudentId))
                {
                    skippedExisting++;
                    continue;
                }

                var newGrade = Common.GradeLevels.Promote(source.GradeLevel);
                var enrollment = new Enrollment
                {
                    StudentId = source.StudentId,
                    SchoolYear = request.ToSchoolYear,
                    GradeLevel = newGrade,
                    Status = EnrollmentStatus.Draft,
                    Remarks = $"Bulk re-enrolled from {request.FromSchoolYear}"
                };
                _context.Enrollments.Add(enrollment);

                foreach (var template in templates.Where(t => t.GradeLevel == null || t.GradeLevel == newGrade))
                {
                    _context.EnrollmentRequirements.Add(new EnrollmentRequirement
                    {
                        EnrollmentId = enrollment.Id,
                        DocumentName = template.DocumentName
                    });
                }

                _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
                {
                    EnrollmentId = enrollment.Id,
                    FromStatus = EnrollmentStatus.Draft,
                    ToStatus = EnrollmentStatus.Draft,
                    Remarks = $"Bulk re-enrolled from {request.FromSchoolYear}"
                });

                created++;
            }

            await _context.SaveChangesAsync(ct);
        }, cancellationToken);

        return new BulkReenrollResultDto(created, skippedExisting, skippedGraduates);
    }
}
