using Enrollify.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Auth.Commands;

/// <summary>
/// Starts the self-service password reset. ALWAYS reports success — whether or not the
/// email has an account — so the endpoint can't be used for account enumeration.
/// The raw token goes only into the emailed link; the user row stores its hash + a 1-hour
/// expiry (one active token per user: a new request overwrites the previous one).
/// When email sending is disabled (dev/demo), the send is logged and skipped — accepted
/// limitation: the reset link is then only recoverable by an operator with log access.
/// </summary>
public record ForgotPasswordCommand(string Email, string FrontendBaseUrl) : IRequest<bool>;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailSender _emailSender;

    public ForgotPasswordCommandHandler(IApplicationDbContext context, IEmailSender emailSender)
    {
        _context = context;
        _emailSender = emailSender;
    }

    public async Task<bool> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // Resolve exactly the way LoginCommand does: tenant-free (IgnoreQueryFilters — auth
        // endpoints run before tenant context exists), email treated as globally unique,
        // active accounts only.
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, cancellationToken);

        if (user == null)
            return true; // Same answer as success — no account enumeration.

        var token = AuthTokens.GenerateUrlSafeToken(32);
        user.PasswordResetTokenHash = AuthTokens.Sha256(token);
        user.PasswordResetTokenExpiresAt = DateTime.UtcNow.Add(AuthTokens.PasswordResetTokenLifetime);

        await _context.SaveChangesAsync(cancellationToken);

        // Best-effort, AFTER the token is persisted — and deliberately NOT awaited: with a
        // real SMTP relay the send takes network time, and awaiting it would make this
        // endpoint measurably slower for existing accounts than unknown ones (a timing side
        // channel defeating the uniform "success" answer). The discarded task carries its own
        // try/catch (SmtpEmailSender never throws by contract; this is defense in depth for
        // any other IEmailSender) and CancellationToken.None so an aborted request can't
        // cancel a send that's already owed.
        var link = $"{request.FrontendBaseUrl.TrimEnd('/')}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={token}";
        var body = $"Hi {user.FirstName},\n\n" +
                   "We received a request to reset your Enrollify password. Open the link below to choose a new one:\n\n" +
                   $"{link}\n\n" +
                   "The link is valid for 1 hour. If you didn't request this, you can ignore this email — your password is unchanged.";
        _ = SendResetEmailSafelyAsync(user.Email, body);

        return true;
    }

    private async Task SendResetEmailSafelyAsync(string to, string body)
    {
        try
        {
            await _emailSender.SendAsync(to, "Reset your Enrollify password", body, CancellationToken.None);
        }
        catch
        {
            // Best-effort by design — a failed reset email must never surface to the caller.
        }
    }
}
