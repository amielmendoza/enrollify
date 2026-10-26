using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record RequestEnrollmentCommand(Guid StudentId, Guid ParentUserId, string? Track = null, string? Strand = null) : IRequest<EnrollmentDto>;

public class RequestEnrollmentCommandHandler : IRequestHandler<RequestEnrollmentCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public RequestEnrollmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EnrollmentDto> Handle(RequestEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

        // Determine the active school year
        var activeSchoolYear = await _context.SchoolYears
            .FirstOrDefaultAsync(sy => sy.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No active school year is configured. Please contact the registrar.");

        var schoolYear = activeSchoolYear.Name;

        // Check if student already has an enrollment for this school year (cancelled ones may be redone)
        var existingEnrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == student.Id && e.SchoolYear == schoolYear
                && e.Status != Domain.Enums.EnrollmentStatus.Cancelled, cancellationToken);

        if (existingEnrollment != null)
            throw new InvalidOperationException($"You already have an enrollment for {schoolYear}.");

        var plan = await EnrollmentRequestPlanner.PlanAsync(_context, student.Id, schoolYear, request.Track, request.Strand, cancellationToken);

        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = schoolYear,
            GradeLevel = plan.GradeLevel,
            LearnerType = plan.LearnerType,
            Track = plan.Track,
            Strand = plan.Strand,
            Status = EnrollmentStatus.Draft,
            Remarks = "Enrollment requested by parent"
        };

        _context.Enrollments.Add(enrollment);

        // Seed requirements from active templates (matching this grade level or applicable to all)
        await EnrollmentRequirements.AddForAsync(_context, enrollment, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, null, null,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt, null,
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
