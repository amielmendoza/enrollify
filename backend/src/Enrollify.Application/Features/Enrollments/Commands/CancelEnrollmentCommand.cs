using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record CancelEnrollmentCommand(Guid EnrollmentId, string? Reason, string CancelledBy) : IRequest<EnrollmentDto>;

public class CancelEnrollmentCommandHandler : IRequestHandler<CancelEnrollmentCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public CancelEnrollmentCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentDto> Handle(CancelEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status == EnrollmentStatus.Cancelled)
            throw new InvalidOperationException("Enrollment is already cancelled.");

        var previousStatus = enrollment.Status;
        enrollment.Status = EnrollmentStatus.Cancelled;
        enrollment.Remarks = request.Reason;

        // Free the seat explicitly so cancelled students never count against section capacity.
        // The section name is preserved in the history remark for the record.
        var freedSectionName = enrollment.Section?.Name;
        enrollment.SectionId = null;
        enrollment.Section = null;

        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = previousStatus,
            ToStatus = EnrollmentStatus.Cancelled,
            Remarks = $"Cancelled by {request.CancelledBy}: {request.Reason ?? "no reason given"}"
                + (freedSectionName != null ? $" (seat freed in section '{freedSectionName}')" : "")
        });

        // Close the books: a cancelled enrollment should end up owing exactly zero, so any
        // outstanding balance is waived with an auto-posted Credit adjustment. The balance
        // composition mirrors GetBalanceQuery/LedgerCalculator exactly — effective total
        // (plan discount/interest via PaymentsCalculator.EffectiveTotal) plus the non-voided
        // adjustment net, minus approved payments — so the ledger bottoms out at what was
        // actually paid. Pre-assessment enrollments have no charges yet, hence nothing to waive.
        if (enrollment.AssessedTotal != null)
        {
            var term = enrollment.PaymentPlan != null
                ? await _context.PaymentTerms.FirstOrDefaultAsync(
                    t => t.SchoolYear == enrollment.SchoolYear && t.PlanType == enrollment.PaymentPlan && t.IsActive, cancellationToken)
                : null;
            var effectiveTotal = PaymentsCalculator.EffectiveTotal(enrollment.PaymentPlan, enrollment.AssessedTotal.Value, term);

            var adjustmentNet = await _context.LedgerAdjustments
                .Where(a => a.EnrollmentId == enrollment.Id && !a.IsVoided)
                .SumAsync(a => a.Type == "Debit" ? a.Amount : -a.Amount, cancellationToken);

            var totalPaid = await _context.Payments
                .Where(p => p.EnrollmentId == enrollment.Id && p.Status == "Approved")
                .SumAsync(p => p.Amount, cancellationToken);

            var outstanding = effectiveTotal + adjustmentNet - totalPaid;
            if (outstanding > 0)
            {
                _context.LedgerAdjustments.Add(new LedgerAdjustment
                {
                    EnrollmentId = enrollment.Id,
                    Type = "Credit",
                    Description = "Cancelled — remaining charges waived",
                    Amount = outstanding,
                    PostedBy = request.CancelledBy
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new EnrollmentDto(enrollment.Id, enrollment.StudentId, enrollment.Student.FullName,
            enrollment.SchoolYear, enrollment.GradeLevel, enrollment.SectionId, enrollment.Section?.Name,
            enrollment.Status, enrollment.Remarks, enrollment.PaymentPlan, enrollment.CreatedAt,
            enrollment.Requirements.Select(r => new RequirementDto(r.Id, r.DocumentName, r.IsSubmitted, r.FileName, r.Notes, r.IsVerified, r.VerifiedBy)).ToList());
    }
}
