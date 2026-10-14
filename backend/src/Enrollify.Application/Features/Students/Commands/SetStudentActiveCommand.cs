using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

/// <summary>
/// Deactivates or reactivates a student record. Deactivation is refused while the student
/// has a non-cancelled enrollment in the ACTIVE school year (deactivating mid-enrollment
/// would strand live money and workflow state); reactivation is always allowed.
/// </summary>
public record SetStudentActiveCommand(Guid StudentId, bool IsActive) : IRequest<StudentDto>;

public class SetStudentActiveCommandHandler : IRequestHandler<SetStudentActiveCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public SetStudentActiveCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<StudentDto> Handle(SetStudentActiveCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        if (!request.IsActive && student.IsActive)
        {
            var activeYear = await _context.SchoolYears
                .Where(sy => sy.IsActive)
                .Select(sy => sy.Name)
                .FirstOrDefaultAsync(cancellationToken);

            if (activeYear != null)
            {
                var hasLiveEnrollment = await _context.Enrollments.AnyAsync(
                    e => e.StudentId == student.Id
                         && e.SchoolYear == activeYear
                         && e.Status != EnrollmentStatus.Cancelled, cancellationToken);
                if (hasLiveEnrollment)
                    throw new InvalidOperationException(
                        $"This student has a non-cancelled enrollment for {activeYear}. Cancel the enrollment first.");
            }
        }

        student.IsActive = request.IsActive;
        await _context.SaveChangesAsync(cancellationToken);

        return StudentMapping.ToDto(student);
    }
}
