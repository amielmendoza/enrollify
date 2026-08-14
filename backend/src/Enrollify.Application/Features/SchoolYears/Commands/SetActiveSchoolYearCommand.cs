using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.SchoolYears;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.SchoolYears.Commands;

public record SetActiveSchoolYearCommand(Guid SchoolYearId) : IRequest<SchoolYearDto>;

public class SetActiveSchoolYearCommandHandler : IRequestHandler<SetActiveSchoolYearCommand, SchoolYearDto>
{
    private readonly IApplicationDbContext _context;

    public SetActiveSchoolYearCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SchoolYearDto> Handle(SetActiveSchoolYearCommand request, CancellationToken cancellationToken)
    {
        var schoolYear = await _context.SchoolYears
            .FirstOrDefaultAsync(sy => sy.Id == request.SchoolYearId, cancellationToken)
            ?? throw new KeyNotFoundException("School year not found.");

        // Deactivate all school years for this tenant
        var activeOnes = await _context.SchoolYears.Where(sy => sy.IsActive).ToListAsync(cancellationToken);
        foreach (var sy in activeOnes)
            sy.IsActive = false;

        schoolYear.IsActive = true;
        await _context.SaveChangesAsync(cancellationToken);

        return new SchoolYearDto(schoolYear.Id, schoolYear.Name, schoolYear.StartDate, schoolYear.EndDate, schoolYear.IsActive, schoolYear.CreatedAt);
    }
}
