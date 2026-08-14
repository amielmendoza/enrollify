using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Posts a manual debit/credit against an enrollment's ledger. Adjustments are add-only:
/// this handler only inserts — corrections are made by voiding and re-posting, never by
/// editing or deleting rows.
/// </summary>
public record PostLedgerAdjustmentCommand(
    Guid EnrollmentId, string Type, string Description, decimal Amount, string PostedBy
) : IRequest<LedgerAdjustmentDto>;

public class PostLedgerAdjustmentCommandValidator : AbstractValidator<PostLedgerAdjustmentCommand>
{
    public PostLedgerAdjustmentCommandValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => t == "Debit" || t == "Credit")
            .WithMessage("Type must be 'Debit' or 'Credit'.");
        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Amount).GreaterThan(0);
    }
}

public class PostLedgerAdjustmentCommandHandler : IRequestHandler<PostLedgerAdjustmentCommand, LedgerAdjustmentDto>
{
    private readonly IApplicationDbContext _context;

    public PostLedgerAdjustmentCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<LedgerAdjustmentDto> Handle(PostLedgerAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        // The ledger opens at assessment — there is nothing to adjust before charges exist.
        if (enrollment.AssessedTotal == null)
            throw new InvalidOperationException("Adjustments can only be posted after assessment.");

        var adjustment = new LedgerAdjustment
        {
            EnrollmentId = enrollment.Id,
            Type = request.Type,
            Description = request.Description.Trim(),
            Amount = request.Amount,
            PostedBy = request.PostedBy
        };

        _context.LedgerAdjustments.Add(adjustment);
        await _context.SaveChangesAsync(cancellationToken);

        return new LedgerAdjustmentDto(adjustment.Id, adjustment.Type, adjustment.Description,
            adjustment.Amount, adjustment.PostedBy, adjustment.CreatedAt);
    }
}
