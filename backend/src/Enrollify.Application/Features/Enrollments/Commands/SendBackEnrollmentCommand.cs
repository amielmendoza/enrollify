using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Sends an Assessed or Approved enrollment back to Submitted for re-assessment (wrong fee
/// set, wrong grade, catalog corrected). The fee snapshot and AssessedTotal are deliberately
/// NOT cleared here: the Submitted→Assessed move-step RemoveRanges the old snapshot and
/// re-snapshots the current catalog, so the next assessment replaces them wholesale.
/// Approved payments survive and keep counting toward the recomputed balance.
/// </summary>
public record SendBackEnrollmentCommand(Guid EnrollmentId, string Reason, string SentBackBy) : IRequest<EnrollmentDto>;

public class SendBackEnrollmentCommandValidator : AbstractValidator<SendBackEnrollmentCommand>
{
    public SendBackEnrollmentCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}

public class SendBackEnrollmentCommandHandler : IRequestHandler<SendBackEnrollmentCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public SendBackEnrollmentCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentDto> Handle(SendBackEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status != EnrollmentStatus.Assessed && enrollment.Status != EnrollmentStatus.Approved)
            throw new InvalidOperationException(
                $"Only assessed or approved enrollments can be sent back for re-assessment. This enrollment is {enrollment.Status}.");

        var previousStatus = enrollment.Status;
        enrollment.Status = EnrollmentStatus.Submitted;

        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = previousStatus,
            ToStatus = EnrollmentStatus.Submitted,
            Remarks = $"Sent back for re-assessment: {request.Reason}",
            CreatedBy = request.SentBackBy
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.Student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, enrollment.SectionId, enrollment.Section?.Name,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt,
            enrollment.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy)).ToList(),
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
