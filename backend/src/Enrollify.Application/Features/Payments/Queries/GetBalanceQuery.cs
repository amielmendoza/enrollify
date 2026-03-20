using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

public record GetBalanceQuery(Guid EnrollmentId) : IRequest<BalanceDto>;

public class GetBalanceQueryHandler : IRequestHandler<GetBalanceQuery, BalanceDto>
{
    private readonly IApplicationDbContext _context;

    public GetBalanceQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BalanceDto> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments.FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        var totalFees = await _context.Fees
            .Where(f => f.SchoolYear == enrollment.SchoolYear && f.GradeLevel == enrollment.GradeLevel && f.IsActive)
            .SumAsync(f => f.Amount, cancellationToken);

        var totalPaid = await _context.Payments
            .Where(p => p.EnrollmentId == request.EnrollmentId)
            .SumAsync(p => p.Amount, cancellationToken);

        return new BalanceDto(totalFees, totalPaid, totalFees - totalPaid);
    }
}
