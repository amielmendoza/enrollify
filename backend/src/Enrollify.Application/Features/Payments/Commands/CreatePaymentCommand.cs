using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Commands;

public record CreatePaymentCommand(
    Guid EnrollmentId, decimal Amount, string PaymentMethod, string? ReferenceNumber, string? Remarks,
    string? ReceiptFileName = null, string? ReceiptFileUrl = null
) : IRequest<PaymentDto>;

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.EnrollmentId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(50);
    }
}

public class CreatePaymentCommandHandler : IRequestHandler<CreatePaymentCommand, PaymentDto>
{
    private readonly IApplicationDbContext _context;

    public CreatePaymentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentDto> Handle(CreatePaymentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments.FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        var payment = new Payment
        {
            EnrollmentId = request.EnrollmentId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = request.ReferenceNumber,
            Remarks = request.Remarks,
            ReceiptFileName = request.ReceiptFileName,
            ReceiptFileUrl = request.ReceiptFileUrl
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        return new PaymentDto(payment.Id, payment.EnrollmentId, payment.Amount,
            payment.PaymentMethod, payment.ReferenceNumber, payment.Remarks, payment.PaymentDate,
            payment.Status, payment.ReviewedBy, payment.ReviewedAt, payment.ReviewNotes,
            payment.ReceiptFileName, payment.ReceiptFileUrl);
    }
}
