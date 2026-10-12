namespace Enrollify.Application.Common.Interfaces;

/// <summary>
/// Best-effort outbound email. Implementations must never throw out of SendAsync —
/// a notification failure must never fail the business operation that triggered it.
/// Some callers (ForgotPasswordCommand) deliberately don't await the send, so it can outlive
/// the HTTP request: implementations must not depend on request-scoped or disposable services
/// such as the DbContext.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}
