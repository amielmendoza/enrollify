using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

// Student self-service enrollment commands. The current user must own the Student record
// via Student.UserId (set by ReviewApplicationCommand for "Student"-type applications).
// Parent-mode enrollments use the per-child commands (RequestEnrollmentCommand etc.) instead.

public record StudentRequestEnrollmentCommand(Guid UserId) : IRequest<EnrollmentDto>;

public class StudentRequestEnrollmentCommandHandler : IRequestHandler<StudentRequestEnrollmentCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;
    public StudentRequestEnrollmentCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentDto> Handle(StudentRequestEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student record not found for this user.");

        var activeSchoolYear = await _context.SchoolYears
            .FirstOrDefaultAsync(sy => sy.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("No active school year is configured. Please contact the registrar.");

        var schoolYear = activeSchoolYear.Name;

        var existing = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == student.Id && e.SchoolYear == schoolYear
                && e.Status != EnrollmentStatus.Cancelled, cancellationToken);
        if (existing != null)
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
            Remarks = "Enrollment requested by student"
        };
        _context.Enrollments.Add(enrollment);

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

public record StudentSubmitEnrollmentCommand(Guid EnrollmentId, Guid UserId) : IRequest<EnrollmentDto>;

public class StudentSubmitEnrollmentCommandHandler : IRequestHandler<StudentSubmitEnrollmentCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;
    public StudentSubmitEnrollmentCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentDto> Handle(StudentSubmitEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        var enrollment = await _context.Enrollments
            .Include(e => e.Student).Include(e => e.Section).Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId && e.StudentId == student.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status != EnrollmentStatus.Draft)
            throw new InvalidOperationException("Enrollment can only be submitted from Draft status.");

        var unsubmitted = enrollment.Requirements.Where(r => !r.IsSubmitted).Select(r => r.DocumentName).ToList();
        if (unsubmitted.Any())
            throw new InvalidOperationException($"Please upload all requirements first: {string.Join(", ", unsubmitted)}");

        enrollment.Status = EnrollmentStatus.Submitted;
        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = EnrollmentStatus.Draft,
            ToStatus = EnrollmentStatus.Submitted,
            Remarks = "Submitted by student"
        });

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.Student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, enrollment.SectionId, enrollment.Section?.Name,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt,
            enrollment.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy)).ToList());
    }
}

public record StudentUploadRequirementCommand(Guid RequirementId, Guid UserId, string FileName, string? FileUrl) : IRequest<bool>;

public class StudentUploadRequirementCommandHandler : IRequestHandler<StudentUploadRequirementCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public StudentUploadRequirementCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(StudentUploadRequirementCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        var requirement = await _context.EnrollmentRequirements
            .Include(r => r.Enrollment)
            .FirstOrDefaultAsync(r => r.Id == request.RequirementId && r.Enrollment.StudentId == student.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Requirement not found.");

        requirement.IsSubmitted = true;
        requirement.FileName = request.FileName;
        requirement.Notes = request.FileUrl;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public record StudentSelectPaymentPlanCommand(Guid UserId, string PaymentPlan) : IRequest<bool>;

public class StudentSelectPaymentPlanCommandHandler : IRequestHandler<StudentSelectPaymentPlanCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public StudentSelectPaymentPlanCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(StudentSelectPaymentPlanCommand request, CancellationToken cancellationToken)
    {
        if (request.PaymentPlan is not ("Full" or "Monthly" or "Quarterly"))
            throw new InvalidOperationException("Invalid payment plan. Must be Full, Monthly, or Quarterly.");

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        var enrollment = await EnrollmentSelector.PickCurrentAsync(_context,
                _context.Enrollments.Where(e => e.StudentId == student.Id), cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status < EnrollmentStatus.Approved)
            throw new InvalidOperationException("Payment plan can only be selected after enrollment is approved.");

        enrollment.PaymentPlan = request.PaymentPlan;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
