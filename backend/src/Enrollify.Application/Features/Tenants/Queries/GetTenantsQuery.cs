using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Tenants.Queries;

/// <summary>
/// Lists all tenants. Intended for SuperAdmin use — bypasses the per-tenant query filter
/// since the operator works across schools.
/// </summary>
public record GetTenantsQuery(bool ActiveOnly = false) : IRequest<List<TenantDto>>;

public class GetTenantsQueryHandler : IRequestHandler<GetTenantsQuery, List<TenantDto>>
{
    private readonly IApplicationDbContext _context;
    public GetTenantsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<TenantDto>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Tenants.IgnoreQueryFilters().AsQueryable();
        if (request.ActiveOnly) query = query.Where(t => t.IsActive);

        var list = await query
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        return list.Select(t => new TenantDto(
            t.Id, t.Name, t.Subdomain, t.ContactEmail, t.ContactPhone, t.Address, t.IsActive, t.CreatedAt))
            .ToList();
    }
}

/// <summary>Public lookup of one tenant — used by /tenants/:id/apply to show the school name.</summary>
public record GetPublicTenantQuery(Guid Id) : IRequest<PublicTenantDto>;

public class GetPublicTenantQueryHandler : IRequestHandler<GetPublicTenantQuery, PublicTenantDto>
{
    private readonly IApplicationDbContext _context;
    public GetPublicTenantQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<PublicTenantDto> Handle(GetPublicTenantQuery request, CancellationToken cancellationToken)
    {
        var t = await _context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("School not found.");

        return new PublicTenantDto(t.Id, t.Name, t.Subdomain);
    }
}

/// <summary>Public list of active tenants — used by the /tenants directory page on the SPA.</summary>
public record GetPublicActiveTenantsQuery : IRequest<List<PublicTenantDto>>;

public class GetPublicActiveTenantsQueryHandler : IRequestHandler<GetPublicActiveTenantsQuery, List<PublicTenantDto>>
{
    private readonly IApplicationDbContext _context;
    public GetPublicActiveTenantsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<PublicTenantDto>> Handle(GetPublicActiveTenantsQuery request, CancellationToken cancellationToken)
    {
        return await _context.Tenants.IgnoreQueryFilters()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .Select(t => new PublicTenantDto(t.Id, t.Name, t.Subdomain))
            .ToListAsync(cancellationToken);
    }
}
