using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ShsStrands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands.Queries;

/// <summary>
/// Active strands for the public, slug-based /apply flow. TenantId comes from the school slug,
/// so the query bypasses the global filter and scopes explicitly (like GetSchoolYearsByTenantQuery).
/// </summary>
public record GetShsStrandsByTenantQuery(Guid TenantId) : IRequest<List<ShsStrandDto>>;

public class GetShsStrandsByTenantQueryHandler : IRequestHandler<GetShsStrandsByTenantQuery, List<ShsStrandDto>>
{
    private readonly IApplicationDbContext _context;

    public GetShsStrandsByTenantQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ShsStrandDto>> Handle(GetShsStrandsByTenantQuery request, CancellationToken cancellationToken)
    {
        return await _context.ShsStrands
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == request.TenantId && s.IsActive)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Track).ThenBy(s => s.Strand)
            .Select(s => new ShsStrandDto(s.Id, s.Track, s.Strand, s.IsActive, s.DisplayOrder))
            .ToListAsync(cancellationToken);
    }
}
