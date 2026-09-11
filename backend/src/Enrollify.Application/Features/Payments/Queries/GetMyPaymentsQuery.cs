using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

/// <summary>
/// The student's own payments view. Shows the requested school year's enrollment when
/// <see cref="SchoolYear"/> is given, else the EnrollmentSelector's current pick, and always
/// reports which year is shown plus the outstanding balance of every other (non-cancelled)
/// year so past dues stay visible after rollover.
/// </summary>
public record GetMyPaymentsQuery(Guid UserId, string? SchoolYear = null) : IRequest<MyPaymentsDto>;

public record OtherYearBalanceDto(string SchoolYear, decimal Balance);

public record MyPaymentsDto(
    BalanceDto Balance,
    List<PaymentDto> Payments,
    string? PaymentPlan,
    List<FeeLineDto> Fees,
    List<InstallmentDto> Schedule,
    decimal? DiscountAmount,
    decimal? InterestAmount,
    string? SchoolYear = null,
    List<OtherYearBalanceDto>? OtherYears = null);

public class GetMyPaymentsQueryHandler : IRequestHandler<GetMyPaymentsQuery, MyPaymentsDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyPaymentsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<MyPaymentsDto> Handle(GetMyPaymentsQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student record not found.");

        // Same shared composition as GetChildPaymentsQuery — the two response twins expose
        // identical schoolYear/otherYears semantics and cannot drift.
        var result = await PaymentsCalculator.BuildStudentViewAsync(_context, student.Id, request.SchoolYear, cancellationToken);
        return new MyPaymentsDto(result.View.Balance, result.View.Payments, result.View.PaymentPlan,
            result.View.Fees, result.View.Schedule, result.View.DiscountAmount, result.View.InterestAmount,
            result.SchoolYear, result.OtherYears);
    }
}
