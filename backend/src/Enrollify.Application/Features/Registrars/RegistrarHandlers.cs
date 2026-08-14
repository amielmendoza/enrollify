using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Tenants;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Registrars;

// All handlers in this file operate within the caller's own tenant — they rely on the global
// per-tenant query filter on User (TenantEntity), so no explicit TenantId parameter is needed.
// The API enforces [Authorize(Roles = "Admin")].

public record GetMyRegistrarsQuery() : IRequest<List<RegistrarDto>>;

public class GetMyRegistrarsQueryHandler : IRequestHandler<GetMyRegistrarsQuery, List<RegistrarDto>>
{
    private readonly IApplicationDbContext _context;
    public GetMyRegistrarsQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<RegistrarDto>> Handle(GetMyRegistrarsQuery request, CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .Where(u => u.Role == UserRole.Registrar)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        return users.Select(u => new RegistrarDto(
            u.Id, u.Email, u.FirstName, u.LastName, u.IsActive, u.CreatedAt))
            .ToList();
    }
}

public record CreateRegistrarCommand(
    string Email, string FirstName, string LastName, string Password
) : IRequest<RegistrarDto>;

public class CreateRegistrarCommandValidator : AbstractValidator<CreateRegistrarCommand>
{
    public CreateRegistrarCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
    }
}

public class CreateRegistrarCommandHandler : IRequestHandler<CreateRegistrarCommand, RegistrarDto>
{
    private readonly IApplicationDbContext _context;
    public CreateRegistrarCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<RegistrarDto> Handle(CreateRegistrarCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Email is globally unique across tenants — server resolves a user's tenant from their record.
        var emailInUse = await _context.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Email == email, cancellationToken);
        if (emailInUse)
            throw new InvalidOperationException("A user with this email already exists.");

        // TenantId is auto-assigned by SaveChangesAsync from the caller's tenant context.
        var user = new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = UserRole.Registrar,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return new RegistrarDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, user.CreatedAt);
    }
}

public record UpdateRegistrarCommand(
    Guid UserId, string FirstName, string LastName, bool IsActive
) : IRequest<RegistrarDto>;

public class UpdateRegistrarCommandHandler : IRequestHandler<UpdateRegistrarCommand, RegistrarDto>
{
    private readonly IApplicationDbContext _context;
    public UpdateRegistrarCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<RegistrarDto> Handle(UpdateRegistrarCommand request, CancellationToken cancellationToken)
    {
        // The tenant filter ensures the Admin can only see registrars in their own tenant.
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.Role == UserRole.Registrar, cancellationToken)
            ?? throw new KeyNotFoundException("Registrar not found in your school.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);

        return new RegistrarDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, user.CreatedAt);
    }
}

public record ResetRegistrarPasswordCommand(Guid UserId, string NewPassword) : IRequest<bool>;

public class ResetRegistrarPasswordCommandValidator : AbstractValidator<ResetRegistrarPasswordCommand>
{
    public ResetRegistrarPasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
    }
}

public class ResetRegistrarPasswordCommandHandler : IRequestHandler<ResetRegistrarPasswordCommand, bool>
{
    private readonly IApplicationDbContext _context;
    public ResetRegistrarPasswordCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(ResetRegistrarPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.Role == UserRole.Registrar, cancellationToken)
            ?? throw new KeyNotFoundException("Registrar not found in your school.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
