using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.SchoolYears;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.SchoolYears.Queries;

public record GetSchoolYearsQuery() : IRequest<List<SchoolYearDto>>;

public class GetSchoolYearsQueryHandler : IRequestHandler<GetSchoolYearsQuery, List<SchoolYearDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSchoolYearsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SchoolYearDto>> Handle(GetSchoolYearsQuery request, CancellationToken cancellationToken)
    {
        return await _context.SchoolYears
            .OrderByDescending(sy => sy.Name)
            .Select(sy => new SchoolYearDto(sy.Id, sy.Name, sy.StartDate, sy.EndDate, sy.IsActive, sy.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
