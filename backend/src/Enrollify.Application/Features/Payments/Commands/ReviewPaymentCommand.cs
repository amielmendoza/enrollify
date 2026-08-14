using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
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

        await _context.SaveChangesAsync(cancellationToken);

        // Best-effort notification after the review has been persisted; IEmailSender never throws.
        await SendReviewOutcomeEmailAsync(payment, cancellationToken);

        return new PaymentDto(payment.Id, payment.EnrollmentId, payment.Amount,
            payment.PaymentMethod, payment.ReferenceNumber, payment.Remarks, payment.PaymentDate,
            payment.Status, payment.ReviewedBy, payment.ReviewedAt, payment.ReviewNotes,
            payment.ReceiptFileName, payment.ReceiptFileUrl);
    }

    private async Task SendReviewOutcomeEmailAsync(Domain.Entities.Payment payment, CancellationToken ct)
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
        if (!string.IsNullOrWhiteSpace(payment.ReviewNotes))
            body += $"\n\nReview notes: {payment.ReviewNotes}";

        await _emailSender.SendAsync(recipient, $"Payment {statusWord}", body, ct);
    }
}
