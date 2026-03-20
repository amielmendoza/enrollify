using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Payments;
using Enrollify.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Commands;

public record StudentPaymentCommand(
    Guid UserId,
    decimal Amount,
    string PaymentMethod,
    string? ReferenceNumber,
    string? Remarks) : IRequest<PaymentDto>;

public class StudentPaymentCommandHandler : IRequestHandler<StudentPaymentCommand, PaymentDto>
{
    private readonly IApplicationDbContext _context;

    public StudentPaymentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentDto> Handle(StudentPaymentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student record not found.");

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == student.Id, cancellationToken)
            ?? throw new KeyNotFoundException("No enrollment found.");

        if (request.Amount <= 0)
            throw new InvalidOperationException("Payment amount must be greater than zero.");

        var payment = new Payment
        {
            EnrollmentId = enrollment.Id,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            ReferenceNumber = request.ReferenceNumber,
            Remarks = request.Remarks
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        return new PaymentDto(payment.Id, payment.EnrollmentId, payment.Amount,
            payment.PaymentMethod, payment.ReferenceNumber, payment.Remarks, payment.PaymentDate);
    }
}
