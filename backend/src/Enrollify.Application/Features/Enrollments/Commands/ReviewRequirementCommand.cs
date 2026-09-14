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
            // uploads while the enrollment is in Draft — so any pre-payment status (Submitted,
            // Assessed, Approved) returns to Draft. The fee snapshot survives and is replaced
            // wholesale on the next Submitted→Assessed step; approved payments keep counting.
            // At Paid/Enrolled/Cancelled the requirement is marked rejected for staff follow-up
            // but the enrollment's status is left alone.
            var status = requirement.Enrollment.Status;
            if (status == EnrollmentStatus.Submitted)
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
            else if (status == EnrollmentStatus.Assessed || status == EnrollmentStatus.Approved)
            {
                requirement.Enrollment.Status = EnrollmentStatus.Draft;
                _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
                {
                    EnrollmentId = requirement.EnrollmentId,
                    FromStatus = status,
                    ToStatus = EnrollmentStatus.Draft,
                    Remarks = $"Requirement '{requirement.DocumentName}' rejected — returned to Draft for re-upload: {request.Notes ?? "no reason given"}"
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
