using Enrollify.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Auth.Commands;

/// <summary>
/// Completes the self-service password reset: validates the emailed token against the
/// stored hash + expiry, sets the new password, clears the token (single-use), clears
/// MustChangePassword, and revokes every active refresh token so stolen sessions die.
/// </summary>
public record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<bool>;

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Token).NotEmpty();
        // Same password rule as RegisterCommand — the two must never drift.
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(6);
    }
}

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private const string InvalidTokenMessage = "This password reset link is invalid or has expired. Request a new one.";

    private readonly IApplicationDbContext _context;

    public ResetPasswordCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Mirror LoginCommand's tenant-free resolution.
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, cancellationToken);

        if (user == null
            || user.PasswordResetTokenHash == null
            || user.PasswordResetTokenExpiresAt == null
            || user.PasswordResetTokenExpiresAt <= DateTime.UtcNow
            || user.PasswordResetTokenHash != AuthTokens.Sha256(request.Token))
        {
            throw new InvalidOperationException(InvalidTokenMessage);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAt = null;
        user.MustChangePassword = false;

        // Whoever prompted the reset (a stolen password, typically) must not keep a session.
        var now = DateTime.UtcNow;
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == user.Id && t.RevokedAt == null && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
            token.RevokedAt = now;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
