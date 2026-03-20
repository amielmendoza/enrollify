using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record SelectPaymentPlanCommand(Guid UserId, string PaymentPlan) : IRequest<bool>;

public class SelectPaymentPlanCommandHandler : IRequestHandler<SelectPaymentPlanCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public SelectPaymentPlanCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(SelectPaymentPlanCommand request, CancellationToken cancellationToken)
    {
        if (request.PaymentPlan is not ("Full" or "Monthly" or "Quarterly"))
            throw new InvalidOperationException("Invalid payment plan. Must be Full, Monthly, or Quarterly.");

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == student.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status < Enrollify.Domain.Enums.EnrollmentStatus.Approved)
            throw new InvalidOperationException("Payment plan can only be selected after enrollment is approved.");

        enrollment.PaymentPlan = request.PaymentPlan;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
