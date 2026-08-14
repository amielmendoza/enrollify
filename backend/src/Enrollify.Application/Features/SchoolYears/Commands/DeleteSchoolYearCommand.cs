using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.SchoolYears.Commands;

public record DeleteSchoolYearCommand(Guid Id) : IRequest<Unit>;

public class DeleteSchoolYearCommandHandler : IRequestHandler<DeleteSchoolYearCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public DeleteSchoolYearCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(DeleteSchoolYearCommand request, CancellationToken cancellationToken)
    {
        var schoolYear = await _context.SchoolYears
            .FirstOrDefaultAsync(sy => sy.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("School year not found.");

        if (schoolYear.IsActive)
            throw new InvalidOperationException("Cannot delete the active school year.");

        var inUse = await _context.Enrollments.AnyAsync(e => e.SchoolYear == schoolYear.Name, cancellationToken)
                 || await _context.Fees.AnyAsync(f => f.SchoolYear == schoolYear.Name, cancellationToken)
                 || await _context.Sections.AnyAsync(s => s.SchoolYear == schoolYear.Name, cancellationToken);

        if (inUse)
            throw new InvalidOperationException("Cannot delete a school year that has enrollments, fees, or sections.");

        _context.SchoolYears.Remove(schoolYear);
        await _context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
