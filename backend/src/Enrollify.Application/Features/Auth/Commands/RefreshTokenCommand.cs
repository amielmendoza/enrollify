using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Auth;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Auth.Commands;

/// <summary>
/// Exchanges a valid refresh token for a fresh JWT + a ROTATED refresh token (the old one
/// is revoked, not deleted, so a replay is detectable). Invalid, revoked, or expired
/// tokens — and deactivated users — get a 401. Opportunistically deletes the user's
/// already-expired tokens so the table doesn't grow without bound.
/// </summary>
public record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResponse>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public RefreshTokenCommandHandler(IApplicationDbContext context, IJwtTokenGenerator tokenGenerator)
    {
        _context = context;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var hash = AuthTokens.Sha256(request.RefreshToken ?? string.Empty);

        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token == null || token.RevokedAt != null || token.ExpiresAt <= now)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // The token row carries no tenant, but User does — resolve tenant-free like
        // LoginCommand (never via Include: the global filter would silently drop the join).
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == token.UserId && u.IsActive, cancellationToken);

        if (user == null)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Rotate: revoke the presented token, issue a fresh one.
        token.RevokedAt = now;
        var (newToken, rawRefreshToken) = AuthTokens.IssueRefreshToken(user.Id);
        _context.RefreshTokens.Add(newToken);

        // Opportunistic cleanup of this user's expired tokens.
        var expired = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && t.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        _context.RefreshTokens.RemoveRange(expired);

        await _context.SaveChangesAsync(cancellationToken);

        // Same claims as login (incl. TenantId), same 8-hour JWT lifetime.
        var jwt = _tokenGenerator.GenerateToken(user);
        return new LoginResponse(jwt, user.Email, user.FullName, user.Role.ToString(), user.TenantId,
            rawRefreshToken, user.MustChangePassword);
    }
}
