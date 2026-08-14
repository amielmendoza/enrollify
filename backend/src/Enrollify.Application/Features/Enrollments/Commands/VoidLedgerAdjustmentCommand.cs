using Enrollify.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Voids a ledger adjustment (audit-safe soft delete). Only the void fields are set —
/// the original row is never edited or removed, and voided rows stay visible on the
/// ledger while being excluded from balance math.
/// </summary>
public record VoidLedgerAdjustmentCommand(
    Guid EnrollmentId, Guid AdjustmentId, string Reason, string VoidedBy
) : IRequest<bool>;

public class VoidLedgerAdjustmentCommandValidator : AbstractValidator<VoidLedgerAdjustmentCommand>
{
    public VoidLedgerAdjustmentCommandValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}

public class VoidLedgerAdjustmentCommandHandler : IRequestHandler<VoidLedgerAdjustmentCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public VoidLedgerAdjustmentCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(VoidLedgerAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var adjustment = await _context.LedgerAdjustments
            .FirstOrDefaultAsync(a => a.Id == request.AdjustmentId && a.EnrollmentId == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Adjustment not found for this enrollment.");

        if (adjustment.IsVoided)
            throw new InvalidOperationException("Adjustment is already voided.");

        adjustment.IsVoided = true;
        adjustment.VoidedBy = request.VoidedBy;
        adjustment.VoidedAt = DateTime.UtcNow;
        adjustment.VoidReason = request.Reason.Trim();

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
