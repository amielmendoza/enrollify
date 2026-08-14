using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Tenants.Queries;

/// <summary>
/// Resolves a public-facing tenant by its subdomain slug. Used by the slug-based public
/// endpoints under /api/schools/{slug}.
/// </summary>
public record GetTenantBySlugQuery(string Slug) : IRequest<PublicTenantDto>;

public class GetTenantBySlugQueryHandler : IRequestHandler<GetTenantBySlugQuery, PublicTenantDto>
{
    private readonly IApplicationDbContext _context;
    public GetTenantBySlugQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PublicTenantDto> Handle(GetTenantBySlugQuery request, CancellationToken cancellationToken)
    {
        var slug = (request.Slug ?? string.Empty).Trim().ToLowerInvariant();
        var t = await _context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Subdomain == slug && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("School not found.");

        return new PublicTenantDto(t.Id, t.Name, t.Subdomain);
    }
}
