using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record UploadRequirementCommand(Guid RequirementId, Guid UserId, string FileName) : IRequest<bool>;

public class UploadRequirementCommandHandler : IRequestHandler<UploadRequirementCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UploadRequirementCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(UploadRequirementCommand request, CancellationToken cancellationToken)
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

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
