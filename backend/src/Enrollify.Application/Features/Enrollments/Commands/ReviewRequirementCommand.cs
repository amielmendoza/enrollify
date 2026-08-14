using Enrollify.Application.Common.Interfaces;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record ReviewRequirementCommand(Guid RequirementId, bool IsVerified, string? Notes, string ReviewerName) : IRequest<bool>;

public class ReviewRequirementCommandHandler : IRequestHandler<ReviewRequirementCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ReviewRequirementCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(ReviewRequirementCommand request, CancellationToken cancellationToken)
    {
        var requirement = await _context.EnrollmentRequirements
            .Include(r => r.Enrollment)
            .FirstOrDefaultAsync(r => r.Id == request.RequirementId, cancellationToken)
            ?? throw new KeyNotFoundException("Requirement not found.");

        if (!requirement.IsSubmitted)
            throw new InvalidOperationException("Cannot review a requirement that has not been submitted.");

        requirement.IsVerified = request.IsVerified;
        requirement.VerifiedBy = request.ReviewerName;
        requirement.VerifiedAt = DateTime.UtcNow;
        requirement.ReviewNotes = request.Notes;

        if (!request.IsVerified)
        {
            requirement.IsSubmitted = false;

            // A rejected document must be re-uploaded, but the parent/student UI only allows
            // uploads while the enrollment is in Draft — so roll a Submitted enrollment back.
            if (requirement.Enrollment.Status == EnrollmentStatus.Submitted)
            {
                requirement.Enrollment.Status = EnrollmentStatus.Draft;
                _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
                {
                    EnrollmentId = requirement.EnrollmentId,
                    FromStatus = EnrollmentStatus.Submitted,
                    ToStatus = EnrollmentStatus.Draft,
                    Remarks = $"Requirement '{requirement.DocumentName}' rejected: {request.Notes ?? "no reason given"}"
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
