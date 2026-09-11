using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record CreateEnrollmentCommand(Guid StudentId, string SchoolYear, string GradeLevel) : IRequest<EnrollmentDto>;

public class CreateEnrollmentCommandValidator : AbstractValidator<CreateEnrollmentCommand>
{
    public CreateEnrollmentCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.SchoolYear).NotEmpty().MaximumLength(20);
        RuleFor(x => x.GradeLevel).NotEmpty().MaximumLength(50);
    }
}

public class CreateEnrollmentCommandHandler : IRequestHandler<CreateEnrollmentCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public CreateEnrollmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EnrollmentDto> Handle(CreateEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        var existingEnrollment = await _context.Enrollments
            .AnyAsync(e => e.StudentId == request.StudentId && e.SchoolYear == request.SchoolYear
                && e.Status != Domain.Enums.EnrollmentStatus.Cancelled, cancellationToken);

        if (existingEnrollment)
            throw new InvalidOperationException("Student already has an enrollment for this school year.");

        var enrollment = new Enrollment
        {
            StudentId = request.StudentId,
            SchoolYear = request.SchoolYear,
            GradeLevel = request.GradeLevel,
            Status = EnrollmentStatus.Draft
        };

        enrollment.StatusHistory.Add(new EnrollmentStatusHistory
        {
            FromStatus = EnrollmentStatus.Draft,
            ToStatus = EnrollmentStatus.Draft,
            Remarks = "Enrollment created"
        });

        _context.Enrollments.Add(enrollment);

        // Seed requirements from the tenant's active templates (grade-filtered), exactly like
        // every other enrollment-creation path — not a hardcoded document list.
        var templates = await _context.RequirementTemplates
            .Where(t => t.IsActive && (t.GradeLevel == null || t.GradeLevel == request.GradeLevel))
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
