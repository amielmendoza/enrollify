using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Enrollments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

/// <summary>
/// Statement of account for one of a parent's children. Ownership is verified via
/// Student.ParentUserId like the sibling parent-context queries.
/// </summary>
public record GetChildLedgerQuery(Guid StudentId, Guid ParentUserId) : IRequest<LedgerDto>;

public class GetChildLedgerQueryHandler : IRequestHandler<GetChildLedgerQuery, LedgerDto>
{
    private readonly IApplicationDbContext _context;

    public GetChildLedgerQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<LedgerDto> Handle(GetChildLedgerQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

        var enrollment = await EnrollmentSelector.PickCurrentAsync(_context,
            _context.Enrollments.Where(e => e.StudentId == student.Id), cancellationToken);

        return enrollment == null
            ? LedgerDto.Empty()
            : await LedgerCalculator.BuildAsync(_context, enrollment.Id, cancellationToken);
    }
}
