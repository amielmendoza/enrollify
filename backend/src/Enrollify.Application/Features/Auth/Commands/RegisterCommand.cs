using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Auth;
using Enrollify.Domain.Entities;
using Enrollify.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Auth.Commands;

public record RegisterCommand(
    string Email, string Password, string FirstName, string LastName, string Role, Guid TenantId
) : IRequest<LoginResponse>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Role).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public RegisterCommandHandler(IApplicationDbContext context, IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<LoginResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Email == request.Email && u.TenantId == request.TenantId, cancellationToken);

        if (exists)
            throw new InvalidOperationException("A user with this email already exists.");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            throw new InvalidOperationException("Invalid role specified.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Role = role,
            TenantId = request.TenantId
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var token = _tokenGenerator.GenerateToken(user);
        return new LoginResponse(token, user.Email, user.FullName, user.Role.ToString(), user.TenantId);
    }
}
