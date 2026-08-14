namespace Enrollify.Application.Common.Interfaces;

/// <summary>
/// Best-effort outbound email. Implementations must never throw out of SendAsync —
/// a notification failure must never fail the business operation that triggered it.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}
