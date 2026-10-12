using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Admissions.Commands;

/// <summary>
/// Marks a submitted application as UnderReview (a registrar has picked it up). Idempotent
/// when already UnderReview; Approved/Rejected applications are final and refuse the move.
/// ReviewApplicationCommand accepts approve/reject from BOTH Submitted and UnderReview,
/// so this state never dead-ends.
/// </summary>
public record SetApplicationUnderReviewCommand(Guid ApplicationId, string Actor) : IRequest<bool>;

public class SetApplicationUnderReviewCommandHandler : IRequestHandler<SetApplicationUnderReviewCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public SetApplicationUnderReviewCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(SetApplicationUnderReviewCommand request, CancellationToken cancellationToken)
    {
        var app = await _context.AdmissionApplications
            .FirstOrDefaultAsync(a => a.Id == request.ApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Application not found.");

        if (app.Status == "UnderReview")
            return true; // idempotent — already being reviewed

        if (app.Status != "Submitted")
            throw new InvalidOperationException(
                $"Only submitted applications can be marked under review. This application is {app.Status}.");

        app.Status = "UnderReview";
        app.ReviewedBy = request.Actor; // who picked it up

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
