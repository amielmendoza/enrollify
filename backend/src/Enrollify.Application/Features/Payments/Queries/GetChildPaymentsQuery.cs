using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

public record GetChildPaymentsQuery(Guid StudentId, Guid ParentUserId) : IRequest<ChildPaymentsDto>;

public record ChildPaymentsDto(
    BalanceDto Balance,
    List<PaymentDto> Payments,
    string? PaymentPlan,
    List<FeeLineDto> Fees,
    List<InstallmentDto> Schedule,
    decimal? DiscountAmount,
    decimal? InterestAmount);

public record FeeLineDto(string Name, string? Description, decimal Amount);

public record InstallmentDto(int Number, string Label, decimal Amount, DateTime DueDate, bool IsPaid);

public class GetChildPaymentsQueryHandler : IRequestHandler<GetChildPaymentsQuery, ChildPaymentsDto>
{
    private readonly IApplicationDbContext _context;

    public GetChildPaymentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ChildPaymentsDto> Handle(GetChildPaymentsQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

        var view = await PaymentsCalculator.BuildAsync(_context, student.Id, cancellationToken);
        return new ChildPaymentsDto(view.Balance, view.Payments, view.PaymentPlan, view.Fees, view.Schedule, view.DiscountAmount, view.InterestAmount);
    }
}
