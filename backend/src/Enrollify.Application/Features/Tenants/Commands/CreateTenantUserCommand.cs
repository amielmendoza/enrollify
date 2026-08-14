using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Tenants;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Tenants.Commands;

public record CreateTenantUserCommand(
    Guid TenantId,
    string Email, string FirstName, string LastName,
    string Role, string Password
) : IRequest<TenantUserDto>;

public class CreateTenantUserCommandValidator : AbstractValidator<CreateTenantUserCommand>
{
    public CreateTenantUserCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        // SuperAdmin can only create Admin users via this command. Registrars are created
        // by tenant Admins through the Admin-scoped /api/registrars endpoint.
        RuleFor(x => x.Role).Must(r => r == "Admin")
            .WithMessage("SuperAdmin can only create Admin users. Registrars are created by tenant Admins.");
    }
}

public class CreateTenantUserCommandHandler : IRequestHandler<CreateTenantUserCommand, TenantUserDto>
{
    private readonly IApplicationDbContext _context;
    public CreateTenantUserCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<TenantUserDto> Handle(CreateTenantUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Tenant must exist (and we want a clear 404 rather than a confusing FK violation).
        var tenantExists = await _context.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == request.TenantId, cancellationToken);
        if (!tenantExists)
            throw new KeyNotFoundException("School not found.");

        var emailInUse = await _context.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email, cancellationToken);
        if (emailInUse)
            throw new InvalidOperationException("A user with this email already exists.");

        if (!Enum.TryParse<UserRole>(request.Role, out var role))
            throw new InvalidOperationException("Invalid role.");

        var user = new User
        {
            TenantId = request.TenantId,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = role,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return new TenantUserDto(user.Id, user.Email, user.FirstName, user.LastName,
            user.Role.ToString(), user.IsActive, user.CreatedAt);
    }
}
