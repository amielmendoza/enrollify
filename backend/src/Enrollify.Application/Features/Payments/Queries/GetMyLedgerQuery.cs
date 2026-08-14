using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Enrollments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

/// <summary>
/// Statement of account for the current student user's current enrollment
/// (picked via EnrollmentSelector like the other self-service flows).
/// </summary>
public record GetMyLedgerQuery(Guid UserId) : IRequest<LedgerDto>;

public class GetMyLedgerQueryHandler : IRequestHandler<GetMyLedgerQuery, LedgerDto>
{
    private readonly IApplicationDbContext _context;

    public GetMyLedgerQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<LedgerDto> Handle(GetMyLedgerQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student record not found for this user.");

        var enrollment = await EnrollmentSelector.PickCurrentAsync(_context,
            _context.Enrollments.Where(e => e.StudentId == student.Id), cancellationToken);

        return enrollment == null
            ? LedgerDto.Empty()
            : await LedgerCalculator.BuildAsync(_context, enrollment.Id, cancellationToken);
    }
}
