using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Enrollments;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

/// <summary>
/// Statement of account for the current student user: the given school year's enrollment
/// when SchoolYear is provided, else the EnrollmentSelector's current pick.
/// </summary>
public record GetMyLedgerQuery(Guid UserId, string? SchoolYear = null) : IRequest<LedgerDto>;

public class GetMyLedgerQueryHandler : IRequestHandler<GetMyLedgerQuery, LedgerDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyLedgerQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<LedgerDto> Handle(GetMyLedgerQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student record not found for this user.");

        var candidates = _context.Enrollments.Where(e => e.StudentId == student.Id);

        var enrollment = !string.IsNullOrWhiteSpace(request.SchoolYear)
            ? await candidates
                .Where(e => e.SchoolYear == request.SchoolYear && e.Status != EnrollmentStatus.Cancelled)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
            : await EnrollmentSelector.PickCurrentAsync(_context, candidates, cancellationToken);

        return enrollment == null
            ? LedgerDto.Empty()
            : await LedgerCalculator.BuildAsync(_context, enrollment.Id, cancellationToken);
    }
}
