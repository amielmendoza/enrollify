using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record AdminUploadRequirementCommand(Guid RequirementId, string FileName, string? FileUrl) : IRequest<bool>;

public class AdminUploadRequirementCommandHandler : IRequestHandler<AdminUploadRequirementCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public AdminUploadRequirementCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(AdminUploadRequirementCommand request, CancellationToken cancellationToken)
    {
        var requirement = await _context.EnrollmentRequirements
            .FirstOrDefaultAsync(r => r.Id == request.RequirementId, cancellationToken)
            ?? throw new KeyNotFoundException("Requirement not found.");

        requirement.IsSubmitted = true;
        requirement.FileName = request.FileName;
        requirement.Notes = request.FileUrl;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
