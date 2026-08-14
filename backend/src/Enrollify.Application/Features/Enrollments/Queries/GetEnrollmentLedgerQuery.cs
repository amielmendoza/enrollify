using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Payments.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Queries;

/// <summary>Admin/registrar statement of account for one enrollment.</summary>
public record GetEnrollmentLedgerQuery(Guid EnrollmentId) : IRequest<LedgerDto>;

public class GetEnrollmentLedgerQueryHandler : IRequestHandler<GetEnrollmentLedgerQuery, LedgerDto>
{
    private readonly IApplicationDbContext _context;

    public GetEnrollmentLedgerQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<LedgerDto> Handle(GetEnrollmentLedgerQuery request, CancellationToken cancellationToken)
    {
        var exists = await _context.Enrollments.AnyAsync(e => e.Id == request.EnrollmentId, cancellationToken);
        if (!exists)
            throw new KeyNotFoundException("Enrollment not found.");

        return await LedgerCalculator.BuildAsync(_context, request.EnrollmentId, cancellationToken);
    }
}
