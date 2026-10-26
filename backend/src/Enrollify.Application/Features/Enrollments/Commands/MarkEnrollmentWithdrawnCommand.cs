using System.Globalization;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Application.Features.Reports.DepEd;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Marks an Approved/Paid/Enrolled enrollment as withdrawn (Dropped or TransferredOut) as of a date.
/// This is a MARK, not a status change: Status, payments and balance are untouched, and the DepEd
/// reports read the mark. A history row (FromStatus = ToStatus = current) records who marked it.
/// </summary>
public record MarkEnrollmentWithdrawnCommand(
    Guid EnrollmentId, string Type, DateTime Date, string? Reason, string? TransferredToSchool, string MarkedBy)
    : IRequest<EnrollmentDto>;

public class MarkEnrollmentWithdrawnCommandValidator : AbstractValidator<MarkEnrollmentWithdrawnCommand>
{
    public const string Dropped = "Dropped";
    public const string TransferredOut = "TransferredOut";

    public MarkEnrollmentWithdrawnCommandValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => t is Dropped or TransferredOut)
            .WithMessage($"Type must be '{Dropped}' or '{TransferredOut}'.");

        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.TransferredToSchool).MaximumLength(200);

        When(x => x.Type == Dropped, () =>
        {
            RuleFor(x => x.Reason).NotEmpty().WithMessage("A reason is required when marking a learner dropped.");
            RuleFor(x => x.TransferredToSchool).Null().WithMessage("A destination school applies only to transfers out.");
        });

        When(x => x.Type == TransferredOut, () =>
        {
            RuleFor(x => x.TransferredToSchool).NotEmpty().WithMessage("The destination school is required when marking a learner transferred out.");
        });
    }
}

public class MarkEnrollmentWithdrawnCommandHandler : IRequestHandler<MarkEnrollmentWithdrawnCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public MarkEnrollmentWithdrawnCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentDto> Handle(MarkEnrollmentWithdrawnCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status is not (EnrollmentStatus.Approved or EnrollmentStatus.Paid or EnrollmentStatus.Enrolled))
            throw new InvalidOperationException(
                $"Only approved, paid or enrolled enrollments can be marked withdrawn. This enrollment is {enrollment.Status}.");

        if (enrollment.WithdrawalType != null)
            throw new InvalidOperationException("This enrollment is already marked withdrawn. Remove the mark first to change it.");

        var schoolYear = await _context.SchoolYears
            .FirstOrDefaultAsync(sy => sy.Name == enrollment.SchoolYear, cancellationToken);

        var date = request.Date.Date;
        var earliest = DepEdRules.EffectiveFirstAttendance(enrollment, schoolYear, enrollment.SchoolYear);
        var today = DepEdRules.ManilaDate(DateTime.UtcNow);

        if (date < earliest)
            throw new InvalidOperationException(
                $"The withdrawal date cannot be before the learner's first attendance ({Fmt(earliest)}).");
        if (date > today)
            throw new InvalidOperationException("The withdrawal date cannot be in the future.");

        var reason = request.Reason?.Trim();
        var school = request.TransferredToSchool?.Trim();
        var dropped = request.Type == MarkEnrollmentWithdrawnCommandValidator.Dropped;

        enrollment.WithdrawalType = request.Type;
        enrollment.WithdrawalDate = date;
        enrollment.WithdrawalReason = string.IsNullOrEmpty(reason) ? null : reason;
        enrollment.TransferredToSchool = dropped || string.IsNullOrEmpty(school) ? null : school;

        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = enrollment.Status,
            ToStatus = enrollment.Status,
            Remarks = dropped
                ? $"Marked dropped as of {Fmt(date)}: {reason}"
                : $"Marked transferred out as of {Fmt(date)} to {school}",
            CreatedBy = request.MarkedBy
        });

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(enrollment);
    }

    internal static string Fmt(DateTime d) => d.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);

    internal static EnrollmentDto ToDto(Enrollment enrollment) =>
        new(enrollment.Id, enrollment.StudentId, enrollment.Student.FullName,
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
