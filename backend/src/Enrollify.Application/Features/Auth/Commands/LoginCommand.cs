using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<LoginResponse>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public LoginCommandHandler(IApplicationDbContext context, IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, cancellationToken);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var token = _tokenGenerator.GenerateToken(user);

        return new LoginResponse(token, user.Email, user.FullName, user.Role.ToString(), user.TenantId);
    }
}
