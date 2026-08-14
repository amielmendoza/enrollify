using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Payments.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Queries;

/// <summary>
/// Fee breakdown for one enrollment. Prefers the snapshot captured at assessment time
/// (AssessedTotal set) so later fee-catalog edits don't retroactively change the lines;
/// enrollments assessed before snapshots existed fall back to the live active Fee catalog,
/// mirroring PaymentsCalculator.BuildAsync.
/// </summary>
public record GetEnrollmentFeesQuery(Guid EnrollmentId) : IRequest<List<FeeLineDto>>;

public class GetEnrollmentFeesQueryHandler : IRequestHandler<GetEnrollmentFeesQuery, List<FeeLineDto>>
{
    private readonly IApplicationDbContext _context;

    public GetEnrollmentFeesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<FeeLineDto>> Handle(GetEnrollmentFeesQuery request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.AssessedTotal.HasValue)
        {
            return await _context.EnrollmentFees
                .Where(f => f.EnrollmentId == enrollment.Id)
                .OrderBy(f => f.Name)
                .Select(f => new FeeLineDto(f.Name, f.Description, f.Amount))
                .ToListAsync(cancellationToken);
        }

        return await _context.Fees
            .Where(f => f.SchoolYear == enrollment.SchoolYear && f.GradeLevel == enrollment.GradeLevel && f.IsActive)
            .OrderBy(f => f.Name)
            .Select(f => new FeeLineDto(f.Name, f.Description, f.Amount))
            .ToListAsync(cancellationToken);
    }
}
