using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.ShsStrands;
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
/// ToSchoolYear enrollment are skipped; so are learners whose source enrollment carries a
/// withdrawal mark (dropped / transferred out). All-or-nothing via a single transaction.
/// </summary>
public record BulkReenrollCommand(string FromSchoolYear, string ToSchoolYear) : IRequest<BulkReenrollResultDto>;

public record BulkReenrollResultDto(int Created, int SkippedExisting, int SkippedGraduates, int NeedsStrand = 0, int SkippedWithdrawn = 0);

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

        int created = 0, skippedExisting = 0, skippedGraduates = 0, needsStrand = 0, skippedWithdrawn = 0;

        var offersStrands = await _context.ShsStrands.AnyAsync(s => s.IsActive, cancellationToken);

        await _context.ExecuteInTransactionAsync(async ct =>
        {
            created = 0; skippedExisting = 0; skippedGraduates = 0; needsStrand = 0; skippedWithdrawn = 0;

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

            var createdEnrollments = new List<Enrollment>();

            foreach (var source in perStudent)
            {
                // Dropped / transferred out during the year: not continuing, so no new Draft.
                if (source.WithdrawalType != null)
                {
                    skippedWithdrawn++;
                    continue;
                }

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
                    LearnerType = "Continuing",
                    // 11 → 12 keeps the strand; 10 → 11 leaves it for the registrar.
                    Track = DepEdLists.IsShsGrade(source.GradeLevel) ? source.Track : null,
                    Strand = DepEdLists.IsShsGrade(source.GradeLevel) ? source.Strand : null,
                    Remarks = $"Bulk re-enrolled from {request.FromSchoolYear}"
                };
                _context.Enrollments.Add(enrollment);
                createdEnrollments.Add(enrollment);
                if (offersStrands && DepEdLists.IsShsGrade(newGrade) && string.IsNullOrWhiteSpace(enrollment.Strand)) needsStrand++;

                _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
                {
                    EnrollmentId = enrollment.Id,
                    FromStatus = EnrollmentStatus.Draft,
                    ToStatus = EnrollmentStatus.Draft,
                    Remarks = $"Bulk re-enrolled from {request.FromSchoolYear}"
                });

                created++;
            }

            await EnrollmentRequirements.AddForManyAsync(_context, createdEnrollments, ct);

            await _context.SaveChangesAsync(ct);
        }, cancellationToken);

        return new BulkReenrollResultDto(created, skippedExisting, skippedGraduates, needsStrand, skippedWithdrawn);
    }
}
