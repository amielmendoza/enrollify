using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Reports.DepEd;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Enrollments.Commands;

/// <summary>
/// Stamps or clears the "encoded in DepEd's LIS" mark on a batch of enrollments. All-or-nothing:
/// every id must exist in the current tenant (the global query filter hides other tenants' rows,
/// so a foreign id reads as missing) and be Listed (Approved/Paid/Enrolled), otherwise the whole
/// request is rejected before anything is changed. Returns the number of enrollments changed:
/// with <c>Encoded = true</c> only unmarked rows are stamped (an existing stamp is never
/// overwritten); with <c>Encoded = false</c> only marked rows are cleared.
/// </summary>
public record SetLisEncodedCommand(IReadOnlyList<Guid> EnrollmentIds, bool Encoded, string EncodedBy) : IRequest<int>;

public class SetLisEncodedCommandValidator : AbstractValidator<SetLisEncodedCommand>
{
    public SetLisEncodedCommandValidator()
    {
        RuleFor(x => x.EnrollmentIds)
            .NotNull()
            .Must(ids => ids != null && ids.Count is >= 1 and <= 500)
                .WithMessage("Select between 1 and 500 enrollments.")
            .Must(ids => ids == null || ids.Distinct().Count() == ids.Count)
                .WithMessage("Enrollment ids must be distinct.");
    }
}

public class SetLisEncodedCommandHandler : IRequestHandler<SetLisEncodedCommand, int>
{
    private readonly IApplicationDbContext _context;

    public SetLisEncodedCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<int> Handle(SetLisEncodedCommand request, CancellationToken cancellationToken)
    {
        var ids = request.EnrollmentIds.Distinct().ToList();

        var enrollments = await _context.Enrollments
            .Where(e => ids.Contains(e.Id))
            .ToListAsync(cancellationToken);

        if (enrollments.Count != ids.Count)
            throw new InvalidOperationException(
                "One or more enrollments were not found. Nothing was changed.");

        var notListed = enrollments.FirstOrDefault(e => !DepEdRules.Listed(e));
        if (notListed != null)
            throw new InvalidOperationException(
                $"Only approved, paid or enrolled enrollments can be marked as encoded in LIS. An enrollment is {notListed.Status}. Nothing was changed.");

        var changed = 0;
        var now = DateTime.UtcNow;
        foreach (var enrollment in enrollments)
        {
            if (request.Encoded)
            {
                if (enrollment.LisEncodedAt != null) continue; // keep the original stamp
                enrollment.LisEncodedAt = now;
                enrollment.LisEncodedBy = request.EncodedBy;
                changed++;
            }
            else
            {
                if (enrollment.LisEncodedAt == null) continue;
                enrollment.LisEncodedAt = null;
                enrollment.LisEncodedBy = null;
                changed++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return changed;
    }
}
