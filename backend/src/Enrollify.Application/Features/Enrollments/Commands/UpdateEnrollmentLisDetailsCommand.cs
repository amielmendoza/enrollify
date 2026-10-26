using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Records the DepEd LIS-facing details staff maintain on an enrollment: the repeater flag and
/// the learner's first attendance date. Editable in every status except Cancelled. A date range
/// check needs the matched SchoolYear row (and the enrollment's withdrawal date), so it lives in
/// the handler rather than a static validator.
/// </summary>
public record UpdateEnrollmentLisDetailsCommand(Guid EnrollmentId, bool IsRepeater, DateTime? FirstAttendanceDate)
    : IRequest<EnrollmentDto>;

public class UpdateEnrollmentLisDetailsCommandHandler : IRequestHandler<UpdateEnrollmentLisDetailsCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateEnrollmentLisDetailsCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentDto> Handle(UpdateEnrollmentLisDetailsCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status == EnrollmentStatus.Cancelled)
            throw new InvalidOperationException("LIS details cannot be edited on a cancelled enrollment.");

        if (request.FirstAttendanceDate.HasValue)
        {
            // Compare calendar dates only — a time-of-day component must not trip the boundaries.
            var date = request.FirstAttendanceDate.Value.Date;

            var schoolYear = await _context.SchoolYears
                .FirstOrDefaultAsync(s => s.Name == enrollment.SchoolYear, cancellationToken)
                ?? throw new InvalidOperationException($"School year '{enrollment.SchoolYear}' was not found.");

            if (date < schoolYear.StartDate.Date)
                throw new InvalidOperationException(
                    $"First attendance date cannot be before the start of school year {schoolYear.Name} ({schoolYear.StartDate:yyyy-MM-dd}).");

            if (date > schoolYear.EndDate.Date)
                throw new InvalidOperationException(
                    $"First attendance date cannot be after the end of school year {schoolYear.Name} ({schoolYear.EndDate:yyyy-MM-dd}).");

            if (enrollment.WithdrawalDate.HasValue && date > enrollment.WithdrawalDate.Value.Date)
                throw new InvalidOperationException(
                    $"First attendance date cannot be after the withdrawal date ({enrollment.WithdrawalDate.Value:yyyy-MM-dd}).");
        }

        enrollment.IsRepeater = request.IsRepeater;
        enrollment.FirstAttendanceDate = request.FirstAttendanceDate?.Date;

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.Student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, enrollment.SectionId, enrollment.Section?.Name,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt,
            enrollment.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy, r.VerifiedAt, r.ReviewNotes)).ToList(),
            StudentNumber: enrollment.Student.StudentNumber,
            Lrn: enrollment.Student.LRN,
            LearnerInfo: EnrollmentLearnerInfoMapping.ToDto(enrollment),
            IsRepeater: enrollment.IsRepeater,
            FirstAttendanceDate: enrollment.FirstAttendanceDate,
            WithdrawalType: enrollment.WithdrawalType,
            WithdrawalDate: enrollment.WithdrawalDate,
            WithdrawalReason: enrollment.WithdrawalReason,
            TransferredToSchool: enrollment.TransferredToSchool,
            LisEncodedAt: enrollment.LisEncodedAt,
            LisEncodedBy: enrollment.LisEncodedBy);
    }
}
