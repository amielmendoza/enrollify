using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Queries;

/// <summary>
/// Status transition trail for one enrollment, oldest first.
/// </summary>
public record GetEnrollmentHistoryQuery(Guid EnrollmentId) : IRequest<List<EnrollmentStatusHistoryDto>>;

public class GetEnrollmentHistoryQueryHandler : IRequestHandler<GetEnrollmentHistoryQuery, List<EnrollmentStatusHistoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEnrollmentHistoryQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<EnrollmentStatusHistoryDto>> Handle(GetEnrollmentHistoryQuery request, CancellationToken cancellationToken)
    {
        var exists = await _context.Enrollments.AnyAsync(e => e.Id == request.EnrollmentId, cancellationToken);
        if (!exists)
            throw new KeyNotFoundException("Enrollment not found.");

        var rows = await _context.EnrollmentStatusHistories
            .Where(h => h.EnrollmentId == request.EnrollmentId)
            .OrderBy(h => h.TransitionDate)
            .ToListAsync(cancellationToken);

        // Map in memory so the enum values reach the client as "Draft"/"Submitted"/... strings.
        return rows
            .Select(h => new EnrollmentStatusHistoryDto(h.FromStatus.ToString(), h.ToStatus.ToString(), h.Remarks, h.TransitionDate))
            .ToList();
    }
}
