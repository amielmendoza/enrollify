using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Commands;

public record ReviewPaymentCommand(Guid PaymentId, bool IsApproved, string? Notes, string ReviewerName) : IRequest<PaymentDto>;

public class ReviewPaymentCommandHandler : IRequestHandler<ReviewPaymentCommand, PaymentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailSender _emailSender;

    public ReviewPaymentCommandHandler(IApplicationDbContext context, IEmailSender emailSender)
    {
        _context = context;
        _emailSender = emailSender;
    }

    public async Task<PaymentDto> Handle(ReviewPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _context.Payments
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new KeyNotFoundException("Payment not found.");

        if (payment.Status != "Pending")
            throw new InvalidOperationException($"Payment is already {payment.Status}.");

        payment.Status = request.IsApproved ? "Approved" : "Rejected";
        payment.ReviewedBy = request.ReviewerName;
        payment.ReviewedAt = DateTime.UtcNow;
        payment.ReviewNotes = request.Notes;

        // Approving a payment can satisfy the Approved→Paid gate — auto-advance the
        // enrollment so the registrar doesn't have to click "Mark as Paid" afterwards.
        // The advance is persisted in the SAME SaveChanges as the review: atomic.
        var autoAdvanced = false;
        if (request.IsApproved)
            autoAdvanced = await TryAutoAdvanceToPaidAsync(payment, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Best-effort notification after the review has been persisted; IEmailSender never throws.
        await SendReviewOutcomeEmailAsync(payment, autoAdvanced, cancellationToken);

        return new PaymentDto(payment.Id, payment.EnrollmentId, payment.Amount,
            payment.PaymentMethod, payment.ReferenceNumber, payment.Remarks, payment.PaymentDate,
            payment.Status, payment.ReviewedBy, payment.ReviewedAt, payment.ReviewNotes,
            payment.ReceiptFileName, payment.ReceiptFileUrl);
    }

    /// <summary>
    /// If the enrollment sits at Approved and this approval pushes the approved-payment total
    /// past the PaymentGate threshold, advance it to Paid with a history row. Rejections never
    /// advance; statuses other than Approved are never touched. The manual Approved→Paid step
    /// in MoveEnrollmentStepCommand remains for offline payments and pre-existing balances.
    /// Nothing is saved here — the caller's SaveChanges persists review + advance atomically.
    /// </summary>
    private async Task<bool> TryAutoAdvanceToPaidAsync(Payment payment, CancellationToken ct)
    {
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == payment.EnrollmentId, ct);

        if (enrollment == null || enrollment.Status != EnrollmentStatus.Approved)
            return false;

        // Same total/term math as the manual gate: assessed snapshot first, live catalog fallback.
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

        // This payment's new "Approved" status isn't saved yet, so sum the others from the
        // store and add this one in memory.
        var previouslyApproved = await _context.Payments
            .Where(p => p.EnrollmentId == enrollment.Id && p.Status == "Approved" && p.Id != payment.Id)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var totalApproved = previouslyApproved + payment.Amount;

        if (totalApproved < PaymentGate.MinimumRequired(enrollment.PaymentPlan, totalFees, term))
            return false;

        enrollment.Status = EnrollmentStatus.Paid;
        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = EnrollmentStatus.Approved,
            ToStatus = EnrollmentStatus.Paid,
            Remarks = "Auto-advanced to Paid on payment approval"
        });
        return true;
    }

    private async Task SendReviewOutcomeEmailAsync(Payment payment, bool autoAdvanced, CancellationToken ct)
    {
        var student = await _context.Enrollments
            .Where(e => e.Id == payment.EnrollmentId)
            .Select(e => e.Student)
            .FirstOrDefaultAsync(ct);

        if (student == null)
            return;

        // Prefer the managing parent's account email when the student was registered by a parent.
        var recipient = student.Email;
        if (student.ParentUserId != null)
        {
            var parentEmail = await _context.Users
                .Where(u => u.Id == student.ParentUserId)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(parentEmail))
                recipient = parentEmail;
        }

        if (string.IsNullOrWhiteSpace(recipient))
            return; // No recipient email — skip.

        var statusWord = payment.Status.ToLowerInvariant();
        var body = $"Your payment of PHP {payment.Amount:N2} ({payment.PaymentMethod}) for {student.FullName} " +
                   $"has been {statusWord}.";
        if (!string.IsNullOrWhiteSpace(payment.ReferenceNumber))
            body += $"\nReference number: {payment.ReferenceNumber}";
        if (autoAdvanced)
            body += "\n\nThis payment completed the required amount — the enrollment has been marked as Paid.";
        if (!string.IsNullOrWhiteSpace(payment.ReviewNotes))
            body += $"\n\nReview notes: {payment.ReviewNotes}";

        await _emailSender.SendAsync(recipient, $"Payment {statusWord}", body, ct);
    }
}
