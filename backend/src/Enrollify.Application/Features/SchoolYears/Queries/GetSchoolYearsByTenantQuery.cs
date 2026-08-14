using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.SchoolYears;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.SchoolYears.Queries;

/// <summary>
/// School years for the public, slug-based /apply flow. TenantId comes from the school slug
/// in the URL (not the tenant provider), so the query bypasses the global filter and scopes
/// explicitly, like the other slug-based handlers.
/// </summary>
public record GetSchoolYearsByTenantQuery(Guid TenantId) : IRequest<List<SchoolYearDto>>;

public class GetSchoolYearsByTenantQueryHandler : IRequestHandler<GetSchoolYearsByTenantQuery, List<SchoolYearDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSchoolYearsByTenantQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<SchoolYearDto>> Handle(GetSchoolYearsByTenantQuery request, CancellationToken cancellationToken)
    {
        return await _context.SchoolYears
            .IgnoreQueryFilters()
            .Where(sy => sy.TenantId == request.TenantId)
            .OrderByDescending(sy => sy.Name)
            .Select(sy => new SchoolYearDto(sy.Id, sy.Name, sy.StartDate, sy.EndDate, sy.IsActive, sy.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
