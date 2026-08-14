using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Tenants;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Tenants.Queries;

/// <summary>
/// Returns Admin users in a tenant. Used by SuperAdmin's per-tenant management page —
/// SuperAdmin only manages Admins; Registrars are created and managed by each tenant's Admin.
/// </summary>
public record GetTenantUsersQuery(Guid TenantId) : IRequest<List<TenantUserDto>>;

public class GetTenantUsersQueryHandler : IRequestHandler<GetTenantUsersQuery, List<TenantUserDto>>
{
    private readonly IApplicationDbContext _context;
    public GetTenantUsersQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<TenantUserDto>> Handle(GetTenantUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _context.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == request.TenantId && u.Role == UserRole.Admin)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        return users.Select(u => new TenantUserDto(
            u.Id, u.Email, u.FirstName, u.LastName, u.Role.ToString(), u.IsActive, u.CreatedAt))
            .ToList();
    }
}
