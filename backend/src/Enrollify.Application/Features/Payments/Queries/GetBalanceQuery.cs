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

        // Prefer the fee total snapshotted at assessment; enrollments assessed before
        // snapshots existed fall back to the live Fee catalog.
        var totalFees = enrollment.AssessedTotal
            ?? await _context.Fees
                .Where(f => f.SchoolYear == enrollment.SchoolYear && f.GradeLevel == enrollment.GradeLevel && f.IsActive)
                .SumAsync(f => f.Amount, cancellationToken);

        var totalPaid = await _context.Payments
            .Where(p => p.EnrollmentId == request.EnrollmentId && p.Status == "Approved")
            .SumAsync(p => p.Amount, cancellationToken);

        // Same math as PaymentsCalculator so this endpoint and the parent/student
        // payment views never disagree on the outstanding balance.
        var term = enrollment.PaymentPlan != null
            ? await _context.PaymentTerms.FirstOrDefaultAsync(
                t => t.SchoolYear == enrollment.SchoolYear && t.PlanType == enrollment.PaymentPlan && t.IsActive, cancellationToken)
            : null;
        var effectiveTotal = PaymentsCalculator.EffectiveTotal(enrollment.PaymentPlan, totalFees, term);

        // Manual ledger adjustments fold into the outstanding balance (voided rows excluded),
        // matching PaymentsCalculator.BuildAsync. TotalFees stays the assessed/catalog figure.
        var adjustmentNet = await _context.LedgerAdjustments
            .Where(a => a.EnrollmentId == request.EnrollmentId && !a.IsVoided)
            .SumAsync(a => a.Type == "Debit" ? a.Amount : -a.Amount, cancellationToken);

        return new BalanceDto(totalFees, totalPaid, effectiveTotal + adjustmentNet - totalPaid);
    }
}
