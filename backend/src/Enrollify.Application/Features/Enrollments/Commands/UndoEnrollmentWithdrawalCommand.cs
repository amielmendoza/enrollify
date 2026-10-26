using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Removes a withdrawal mark (marked in error, or the learner returned): clears all four
/// withdrawal fields and records a history row. Status, payments and balance are untouched.
/// </summary>
public record UndoEnrollmentWithdrawalCommand(Guid EnrollmentId, string UndoneBy) : IRequest<EnrollmentDto>;

public class UndoEnrollmentWithdrawalCommandHandler : IRequestHandler<UndoEnrollmentWithdrawalCommand, EnrollmentDto>
{
    private readonly IApplicationDbContext _context;

    public UndoEnrollmentWithdrawalCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<EnrollmentDto> Handle(UndoEnrollmentWithdrawalCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Section)
            .Include(e => e.Requirements)
            .FirstOrDefaultAsync(e => e.Id == request.EnrollmentId, cancellationToken)
            ?? throw new KeyNotFoundException("Enrollment not found.");

        if (enrollment.WithdrawalType == null)
            throw new InvalidOperationException("This enrollment is not marked withdrawn.");

        var was = enrollment.WithdrawalType == "Dropped" ? "dropped" : "transferred out";
        var asOf = enrollment.WithdrawalDate is { } d
            ? $" as of {MarkEnrollmentWithdrawnCommandHandler.Fmt(d)}"
            : string.Empty;

        enrollment.WithdrawalType = null;
        enrollment.WithdrawalDate = null;
        enrollment.WithdrawalReason = null;
        enrollment.TransferredToSchool = null;

        _context.EnrollmentStatusHistories.Add(new EnrollmentStatusHistory
        {
            EnrollmentId = enrollment.Id,
            FromStatus = enrollment.Status,
            ToStatus = enrollment.Status,
            Remarks = $"Withdrawal mark removed (was {was}{asOf})",
            CreatedBy = request.UndoneBy
        });

        await _context.SaveChangesAsync(cancellationToken);

        return MarkEnrollmentWithdrawnCommandHandler.ToDto(enrollment);
    }
}
