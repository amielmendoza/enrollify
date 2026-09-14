using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Commands;

/// <summary>
/// Voids an APPROVED payment (recorded in error, bounced after verification, refunded).
/// Audit-safe: the row is kept and marked, never deleted. Voiding drops the amount out of
/// every money sum (they all filter Status == "Approved"), and if that breaks the
/// Approved→Paid gate for an enrollment still sitting at Paid, the enrollment is rolled
/// back to Approved in the same save. An Enrolled enrollment's status is never touched —
/// the reopened debt surfaces through the balance instead.
/// </summary>
public record VoidPaymentCommand(Guid PaymentId, string Reason, string VoidedBy) : IRequest<PaymentDto>;

public class VoidPaymentCommandValidator : AbstractValidator<VoidPaymentCommand>
{
    public VoidPaymentCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}

public class VoidPaymentCommandHandler : IRequestHandler<VoidPaymentCommand, PaymentDto>
{
    private readonly IApplicationDbContext _context;

    public VoidPaymentCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<PaymentDto> Handle(VoidPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment not found.");

        if (payment.Status == "Voided")
            throw new InvalidOperationException("Payment is already voided.");
        if (payment.Status == "Pending")
            throw new InvalidOperationException("Pending payments cannot be voided. Reject it via review instead.");
        if (payment.Status != "Approved")
            throw new InvalidOperationException($"Only approved payments can be voided. This payment is {payment.Status}.");

        payment.Status = "Voided";
        payment.VoidedBy = request.VoidedBy;
        payment.VoidedAt = DateTime.UtcNow;
        payment.VoidReason = request.Reason.Trim();

        var enrollmentStatus = await _context.Enrollments
            .Where(e => e.Id == payment.EnrollmentId)
            .Select(e => (EnrollmentStatus?)e.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (enrollmentStatus == EnrollmentStatus.Cancelled)
        {
            // Cancel-then-void: the cancellation waiver was sized against the Approved sum at
            // cancel time, so voiding a payment afterwards would reopen debt that nothing can
            // heal (a cancelled enrollment's books are closed to new manual postings). Post the
            // compensating credit directly here — bypassing the adjustment COMMAND's Cancelled
            // guard by design — so a cancelled account always owes exactly zero.
            _context.LedgerAdjustments.Add(new LedgerAdjustment
            {
                EnrollmentId = payment.EnrollmentId,
                Type = "Credit",
                Description = $"Waiver adjusted — payment of ₱{payment.Amount:N2} voided after cancellation",
                Amount = payment.Amount,
                PostedBy = request.VoidedBy
            });
        }
        else
        {
            await RollBackPaidStatusIfGateBrokenAsync(payment, request.Reason, cancellationToken);
        }

        // One save: the void plus any Paid→Approved rollback or compensating waiver credit
        // commit together.
        await _context.SaveChangesAsync(cancellationToken);

        return new PaymentDto(payment.Id, payment.EnrollmentId, payment.Amount,
            payment.PaymentMethod, payment.ReferenceNumber, payment.Remarks, payment.PaymentDate,
            payment.Status, payment.ReviewedBy, payment.ReviewedAt, payment.ReviewNotes,
            payment.ReceiptFileName, payment.ReceiptFileUrl,
            payment.VoidedBy, payment.VoidedAt, payment.VoidReason);
    }

    /// <summary>
    /// Mirrors ReviewPaymentCommand.TryAutoAdvanceToPaidAsync's inputs (assessed snapshot
    /// first, live catalog fallback; the tenant's active term; PaymentGate threshold), just
    /// in the opposite direction. Only a Paid enrollment can regress; Enrolled never does.
    /// </summary>
    private async Task RollBackPaidStatusIfGateBrokenAsync(Payment payment, string reason, CancellationToken ct)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Payments)
            .FirstOrDefaultAsync(e => e.Id == payment.EnrollmentId, ct);

        if (enrollment == null || enrollment.Status != EnrollmentStatus.Paid)
            return;

        decimal totalFees;
        if (enrollment.AssessedTotal.HasValue)
        {
            totalFees = enrollment.AssessedTotal.Value;
        }
        else
        {
            totalFees = await _context.Fees
                .Where(f => f.SchoolYear == enrollment.SchoolYear && f.GradeLevel == enrollment.GradeLevel && f.IsActive)
                .SumAsync(f => (decimal?)f.Amount, ct) ?? 0m;
        }

        var term = enrollment.PaymentPlan != null
            ? await _context.PaymentTerms.FirstOrDefaultAsync(
                t => t.SchoolYear == enrollment.SchoolYear && t.PlanType == enrollment.PaymentPlan && t.IsActive, ct)
            : null;

        // The voided payment is the same tracked instance inside enrollment.Payments, so its
        // new "Voided" status already excludes it from this in-memory approved sum.
        var approvedTotal = enrollment.Payments.Where(p => p.Status == "Approved").Sum(p => p.Amount);

        if (approvedTotal >= PaymentGate.MinimumRequired(enrollment.PaymentPlan, totalFees, term))
            return;

        enrollment.Status = EnrollmentStatus.Approved;
        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = EnrollmentStatus.Paid,
            ToStatus = EnrollmentStatus.Approved,
            Remarks = $"Rolled back to Approved: payment of ₱{payment.Amount:N2} voided ({reason})"
        });
    }
}
