using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record AssignSectionCommand(Guid EnrollmentId, Guid SectionId) : IRequest<EnrollmentDto>;

public class AssignSectionCommandValidator : AbstractValidator<AssignSectionCommand>
{
    public AssignSectionCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.SectionId).NotEmpty();
    }
}

public class AssignSectionCommandHandler : IRequestHandler<AssignSectionCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public AssignSectionCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EnrollmentDto> Handle(AssignSectionCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        var section = await _context.Sections
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == request.SectionId, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        if (section.IsFull)
            throw new InvalidOperationException($"Section '{section.Name}' is already at full capacity ({section.Capacity}).");

        enrollment.SectionId = request.SectionId;
        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.Student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, section.Id, section.Name,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt, null);
    }
}
