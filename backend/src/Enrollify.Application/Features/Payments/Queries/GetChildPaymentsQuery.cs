using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

public record GetChildPaymentsQuery(Guid StudentId, Guid ParentUserId, string? SchoolYear = null) : IRequest<ChildPaymentsDto>;

public record ChildPaymentsDto(
    BalanceDto Balance,
    List<PaymentDto> Payments,
    string? PaymentPlan,
    List<FeeLineDto> Fees,
    List<InstallmentDto> Schedule,
    decimal? DiscountAmount,
    decimal? InterestAmount,
    string? SchoolYear = null,
    List<OtherYearBalanceDto>? OtherYears = null);

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

        // Same shared composition as GetMyPaymentsQuery — the two response twins expose
        // identical schoolYear/otherYears semantics and cannot drift.
        var result = await PaymentsCalculator.BuildStudentViewAsync(_context, student.Id, request.SchoolYear, cancellationToken);
        return new ChildPaymentsDto(result.View.Balance, result.View.Payments, result.View.PaymentPlan,
            result.View.Fees, result.View.Schedule, result.View.DiscountAmount, result.View.InterestAmount,
            result.SchoolYear, result.OtherYears);
    }
}
