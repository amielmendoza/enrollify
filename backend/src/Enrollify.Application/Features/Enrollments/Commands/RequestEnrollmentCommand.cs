using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record RequestEnrollmentCommand(Guid StudentId, Guid ParentUserId) : IRequest<EnrollmentDto>;

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

        // Grade resolution order: (1) promote from the most recent non-cancelled enrollment
        // in a different school year (re-enrollment); (2) the approved application's grade
        // (first-time enrollment); (3) "Grade 7" as a last resort.
        var previousEnrollment = await _context.Enrollments
            .Where(e => e.StudentId == student.Id
                && e.SchoolYear != schoolYear
                && e.Status != EnrollmentStatus.Cancelled)
            .OrderByDescending(e => e.SchoolYear).ThenByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        string gradeLevel;
        if (previousEnrollment != null)
        {
            gradeLevel = Common.GradeLevels.Promote(previousEnrollment.GradeLevel);
        }
        else
        {
            var application = await _context.AdmissionApplications
                .FirstOrDefaultAsync(a => a.StudentId == student.Id && a.Status == "Approved", cancellationToken);
            gradeLevel = application?.GradeLevel ?? "Grade 7";
        }

        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = schoolYear,
            GradeLevel = gradeLevel,
            Status = EnrollmentStatus.Draft,
            Remarks = "Enrollment requested by parent"
        };

        _context.Enrollments.Add(enrollment);

        // Seed requirements from active templates (matching this grade level or applicable to all)
        var templates = await _context.RequirementTemplates
            .Where(t => t.IsActive && (t.GradeLevel == null || t.GradeLevel == gradeLevel))
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.DocumentName)
            .ToListAsync(cancellationToken);

        foreach (var template in templates)
        {
            _context.EnrollmentRequirements.Add(new EnrollmentRequirement
            {
                EnrollmentId = enrollment.Id,
                DocumentName = template.DocumentName
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, null, null,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt, null);
    }
}
