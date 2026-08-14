using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.PaymentTerms;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.PaymentTerms.Queries;

public record GetPaymentTermsQuery(string? SchoolYear) : IRequest<List<PaymentTermDto>>;

public class GetPaymentTermsQueryHandler : IRequestHandler<GetPaymentTermsQuery, List<PaymentTermDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPaymentTermsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<PaymentTermDto>> Handle(GetPaymentTermsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PaymentTerms.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SchoolYear))
            query = query.Where(p => p.SchoolYear == request.SchoolYear);

        return await query
            .OrderBy(p => p.SchoolYear).ThenBy(p => p.PlanType)
            .Select(p => new PaymentTermDto(p.Id, p.SchoolYear, p.PlanType,
                p.DownPaymentPercent, p.InterestRatePercent, p.DiscountPercent,
                p.InstallmentCount, p.IsActive))
            .ToListAsync(cancellationToken);
    }
}
