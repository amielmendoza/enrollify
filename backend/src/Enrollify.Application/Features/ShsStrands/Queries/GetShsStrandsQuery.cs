using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ShsStrands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ShsStrands.Queries;

public record GetShsStrandsQuery(bool IncludeInactive = false) : IRequest<List<ShsStrandDto>>;

public class GetShsStrandsQueryHandler : IRequestHandler<GetShsStrandsQuery, List<ShsStrandDto>>
{
    private readonly IApplicationDbContext _context;

    public GetShsStrandsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<ShsStrandDto>> Handle(GetShsStrandsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ShsStrands.AsQueryable();
        if (!request.IncludeInactive)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Track).ThenBy(s => s.Strand)
            .Select(s => new ShsStrandDto(s.Id, s.Track, s.Strand, s.IsActive, s.DisplayOrder))
            .ToListAsync(cancellationToken);
    }
}
