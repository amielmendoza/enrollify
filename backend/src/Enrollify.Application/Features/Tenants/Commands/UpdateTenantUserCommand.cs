using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Tenants;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Tenants.Commands;

/// <summary>
/// SuperAdmin updates an Admin in a tenant. Role is fixed (Admin); only name and active state
/// can be changed. To change someone's role, deactivate them and create a new account.
/// </summary>
public record UpdateTenantUserCommand(
    Guid TenantId, Guid UserId,
    string FirstName, string LastName, bool IsActive
) : IRequest<TenantUserDto>;

public class UpdateTenantUserCommandHandler : IRequestHandler<UpdateTenantUserCommand, TenantUserDto>
{
    private readonly IApplicationDbContext _context;
    public UpdateTenantUserCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<TenantUserDto> Handle(UpdateTenantUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.TenantId == request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("User not found in this school.");

        if (user.Role != UserRole.Admin)
            throw new InvalidOperationException("Only Admin users are managed here. Registrars are managed by tenant Admins.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return new TenantUserDto(user.Id, user.Email, user.FirstName, user.LastName,
            user.Role.ToString(), user.IsActive, user.CreatedAt);
    }
}
