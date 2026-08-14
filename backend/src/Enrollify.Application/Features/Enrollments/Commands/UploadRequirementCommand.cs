using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record UploadRequirementCommand(Guid RequirementId, Guid StudentId, Guid ParentUserId, string FileName, string? FileUrl) : IRequest<bool>;

public class UploadRequirementCommandHandler : IRequestHandler<UploadRequirementCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public UploadRequirementCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(UploadRequirementCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

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
