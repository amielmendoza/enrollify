using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Application.Features.ShsStrands;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record CreateEnrollmentCommand(Guid StudentId, string SchoolYear, string GradeLevel, string? LearnerType = null, string? Track = null, string? Strand = null) : IRequest<EnrollmentDto>;

public class CreateEnrollmentCommandValidator : AbstractValidator<CreateEnrollmentCommand>
{
    public CreateEnrollmentCommandValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.SchoolYear).NotEmpty().MaximumLength(20);
        RuleFor(x => x.GradeLevel).NotEmpty().MaximumLength(50);
        RuleFor(x => x.LearnerType).Must(t => DepEdLists.LearnerTypes.Contains(t!.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.LearnerType))
            .WithMessage("Learner type must be New, Continuing, Transferee, or Returning.");
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

        var (track, strand) = await ShsStrandRules.ResolveAsync(_context, request.GradeLevel, request.Track, request.Strand, cancellationToken);
        var learnerType = string.IsNullOrWhiteSpace(request.LearnerType)
            ? (await _context.Enrollments.AnyAsync(e => e.StudentId == request.StudentId && e.Status != EnrollmentStatus.Cancelled, cancellationToken)
                ? "Continuing" : "New")
            : request.LearnerType.Trim();

        var enrollment = new Enrollment
        {
            StudentId = request.StudentId,
            SchoolYear = request.SchoolYear,
            GradeLevel = request.GradeLevel,
            LearnerType = learnerType,
            Track = track,
            Strand = strand,
            Status = EnrollmentStatus.Draft
        };

        enrollment.StatusHistory.Add(new EnrollmentStatusHistory
        {
            FromStatus = EnrollmentStatus.Draft,
            ToStatus = EnrollmentStatus.Draft,
            Remarks = "Enrollment created"
        });

        _context.Enrollments.Add(enrollment);

        // Seed requirements from the tenant's active templates via the shared builder, exactly like
        // every other enrollment-creation path (grade-filtered; keep-on-file documents carry over).
        await EnrollmentRequirements.AddForAsync(_context, enrollment, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, null, null,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt, null);
    }
}
