using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

public record SelectPaymentPlanCommand(Guid StudentId, Guid ParentUserId, string PaymentPlan) : IRequest<bool>;

public class SelectPaymentPlanCommandHandler : IRequestHandler<SelectPaymentPlanCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public SelectPaymentPlanCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(SelectPaymentPlanCommand request, CancellationToken cancellationToken)
    {
        if (request.PaymentPlan is not ("Full" or "Monthly" or "Quarterly"))
            throw new InvalidOperationException("Invalid payment plan. Must be Full, Monthly, or Quarterly.");

        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

        var enrollment = await EnrollmentSelector.PickCurrentAsync(_context,
                _context.Enrollments.Where(e => e.StudentId == student.Id), cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.Status < Enrollify.Domain.Enums.EnrollmentStatus.Approved)
            throw new InvalidOperationException("Payment plan can only be selected after enrollment is approved.");

        enrollment.PaymentPlan = request.PaymentPlan;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
