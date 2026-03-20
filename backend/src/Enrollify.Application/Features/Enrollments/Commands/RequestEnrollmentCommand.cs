using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record RequestEnrollmentCommand(Guid UserId) : IRequest<EnrollmentDto>;

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
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student record not found for this user.");

        // Check if student already has an active enrollment
        var existingEnrollment = await _context.Enrollments
            .Include(e => e.Section)
            .FirstOrDefaultAsync(e => e.StudentId == student.Id, cancellationToken);

        if (existingEnrollment != null)
            throw new InvalidOperationException("You already have an active enrollment.");

        // Find the approved application to get grade level and school year
        var application = await _context.AdmissionApplications
            .FirstOrDefaultAsync(a => a.StudentId == student.Id && a.Status == "Approved", cancellationToken);

        var gradeLevel = application?.GradeLevel ?? "Grade 7";
        var schoolYear = application?.SchoolYear ?? "2024-2025";

        var enrollment = new Enrollment
        {
            StudentId = student.Id,
            SchoolYear = schoolYear,
            GradeLevel = gradeLevel,
            Status = EnrollmentStatus.Draft,
            Remarks = "Enrollment requested by student"
        };

        _context.Enrollments.Add(enrollment);

        // Create default requirements
        var defaultRequirements = new List<string>
        {
            "PSA Birth Certificate",
            "Form 138 (Report Card)",
            "Good Moral Certificate",
            "2x2 ID Photo"
        };

        foreach (var docName in defaultRequirements)
        {
            _context.EnrollmentRequirements.Add(new EnrollmentRequirement
            {
                EnrollmentId = enrollment.Id,
                DocumentName = docName
            });
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, null, null,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt, null);
    }
}
