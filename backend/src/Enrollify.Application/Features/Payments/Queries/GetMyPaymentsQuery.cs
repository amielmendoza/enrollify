using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

public record GetMyPaymentsQuery(Guid UserId) : IRequest<MyPaymentsDto>;

public record MyPaymentsDto(
    BalanceDto Balance,
    List<PaymentDto> Payments,
    string? PaymentPlan,
    List<FeeLineDto> Fees,
    List<InstallmentDto> Schedule,
    decimal? DiscountAmount,
    decimal? InterestAmount);

public class GetMyPaymentsQueryHandler : IRequestHandler<GetMyPaymentsQuery, MyPaymentsDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyPaymentsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<MyPaymentsDto> Handle(GetMyPaymentsQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student record not found.");

        var dto = await PaymentsCalculator.BuildAsync(_context, student.Id, cancellationToken);
        return new MyPaymentsDto(dto.Balance, dto.Payments, dto.PaymentPlan, dto.Fees, dto.Schedule, dto.DiscountAmount, dto.InterestAmount);
    }
}
