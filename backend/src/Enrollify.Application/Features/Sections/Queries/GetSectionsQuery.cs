using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Sections;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Sections.Queries;

public record GetSectionsQuery(string? SchoolYear, string? GradeLevel, bool IncludeInactive = false) : IRequest<List<SectionDto>>;

public class GetSectionsQueryHandler : IRequestHandler<GetSectionsQuery, List<SectionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSectionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SectionDto>> Handle(GetSectionsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Sections.Include(s => s.Enrollments).AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(s => s.IsActive);

        if (!string.IsNullOrWhiteSpace(request.SchoolYear))
            query = query.Where(s => s.SchoolYear == request.SchoolYear);

        if (!string.IsNullOrWhiteSpace(request.GradeLevel))
            query = query.Where(s => s.GradeLevel == request.GradeLevel);

        return await query
            .OrderBy(s => s.GradeLevel).ThenBy(s => s.Name)
            .Select(s => new SectionDto(s.Id, s.Name, s.GradeLevel, s.SchoolYear,
                s.Capacity, s.Enrollments.Count, s.Adviser, s.IsActive))
            .ToListAsync(cancellationToken);
    }
}
