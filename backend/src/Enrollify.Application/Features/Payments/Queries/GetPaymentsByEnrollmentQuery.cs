using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

public record GetPaymentsByEnrollmentQuery(Guid EnrollmentId) : IRequest<List<PaymentDto>>;

public class GetPaymentsByEnrollmentQueryHandler : IRequestHandler<GetPaymentsByEnrollmentQuery, List<PaymentDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPaymentsByEnrollmentQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<PaymentDto>> Handle(GetPaymentsByEnrollmentQuery request, CancellationToken cancellationToken)
    {
        return await _context.Payments
            .Where(p => p.EnrollmentId == request.EnrollmentId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentDto(p.Id, p.EnrollmentId, p.Amount, p.PaymentMethod, p.ReferenceNumber, p.Remarks, p.PaymentDate))
            .ToListAsync(cancellationToken);
    }
}
