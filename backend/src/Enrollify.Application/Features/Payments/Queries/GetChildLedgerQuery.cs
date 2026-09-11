using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.Features.Enrollments;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Payments.Queries;

/// <summary>
/// Statement of account for one of a parent's children (ownership via Student.ParentUserId).
/// Honors an explicit SchoolYear; otherwise shows the EnrollmentSelector's current pick.
/// </summary>
public record GetChildLedgerQuery(Guid StudentId, Guid ParentUserId, string? SchoolYear = null) : IRequest<LedgerDto>;

public class GetChildLedgerQueryHandler : IRequestHandler<GetChildLedgerQuery, LedgerDto>
{
    private readonly IApplicationDbContext _context;

    public GetChildLedgerQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<LedgerDto> Handle(GetChildLedgerQuery request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

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
