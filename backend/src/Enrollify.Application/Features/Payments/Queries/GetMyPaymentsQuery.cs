using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

public record GetMyPaymentsQuery(Guid UserId) : IRequest<MyPaymentsDto>;

public record MyPaymentsDto(
    BalanceDto Balance,
    List<PaymentDto> Payments);

public class GetMyPaymentsQueryHandler : IRequestHandler<GetMyPaymentsQuery, MyPaymentsDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyPaymentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<MyPaymentsDto> Handle(GetMyPaymentsQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student record not found.");

        var enrollment = await _context.Enrollments
            .Include(e => e.Payments)
            .FirstOrDefaultAsync(e => e.StudentId == student.Id, cancellationToken);

        if (enrollment == null)
            return new MyPaymentsDto(new BalanceDto(0, 0, 0), new List<PaymentDto>());

        var totalFees = await _context.Fees
            .Where(f => f.SchoolYear == enrollment.SchoolYear && f.GradeLevel == enrollment.GradeLevel && f.IsActive)
            .SumAsync(f => f.Amount, cancellationToken);

        var totalPaid = enrollment.Payments.Sum(p => p.Amount);

        var balance = new BalanceDto(totalFees, totalPaid, totalFees - totalPaid);

        var payments = enrollment.Payments
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentDto(p.Id, p.EnrollmentId, p.Amount,
                p.PaymentMethod, p.ReferenceNumber, p.Remarks, p.PaymentDate))
            .ToList();

        return new MyPaymentsDto(balance, payments);
    }
}
