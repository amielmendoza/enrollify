using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Tenants;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Tenants.Commands;

public record UpdateTenantCommand(
    Guid Id, string Name, string Subdomain,
    string? ContactEmail, string? ContactPhone, string? Address, bool IsActive
) : IRequest<TenantDto>;

public class UpdateTenantCommandHandler : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    private readonly IApplicationDbContext _context;
    public UpdateTenantCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<TenantDto> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("School not found.");

        var sub = request.Subdomain.Trim().ToLowerInvariant();
        if (ReservedSubdomains.IsReserved(sub))
            throw new InvalidOperationException($"'{sub}' is a reserved word and can't be used as a subdomain.");

        var conflict = await _context.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Id != request.Id && t.Subdomain == sub, cancellationToken);
        if (conflict) throw new InvalidOperationException($"Subdomain '{sub}' is already in use.");

        tenant.Name = request.Name.Trim();
        tenant.Subdomain = sub;
        tenant.ContactEmail = request.ContactEmail;
        tenant.ContactPhone = request.ContactPhone;
        tenant.Address = request.Address;
        tenant.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return new TenantDto(tenant.Id, tenant.Name, tenant.Subdomain, tenant.ContactEmail,
            tenant.ContactPhone, tenant.Address, tenant.IsActive, tenant.CreatedAt);
    }
}
