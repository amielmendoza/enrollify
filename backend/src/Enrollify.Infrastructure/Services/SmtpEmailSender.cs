using System.Net;
using System.Net.Mail;
using Enrollify.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Enrollify.Infrastructure.Services;

/// <summary>
/// Minimal SMTP sender for MVP notifications, configured via the "Email" section
/// (Host, Port, UserName, Password, FromAddress, FromName, Enabled).
/// Disabled by default so dev/demo environments need zero setup: when Enabled is false
/// or Host is empty the send is logged and skipped. Never throws out of SendAsync.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        try
        {
            var section = _configuration.GetSection("Email");
            var enabled = string.Equals(section["Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            var host = section["Host"];

            if (!enabled || string.IsNullOrWhiteSpace(host))
            {
                _logger.LogInformation("Email sending disabled — skipped email to {To} with subject '{Subject}'.", to, subject);
                return;
            }

            var port = int.TryParse(section["Port"], out var parsedPort) ? parsedPort : 587;
            var userName = section["UserName"];
            var password = section["Password"];
            var fromAddress = section["FromAddress"];
            var fromName = section["FromName"];

            if (string.IsNullOrWhiteSpace(fromAddress))
                fromAddress = string.IsNullOrWhiteSpace(userName) ? "no-reply@enrollify.local" : userName;

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress, string.IsNullOrWhiteSpace(fromName) ? "Enrollify" : fromName),
                Subject = subject,
                Body = body
            };
            message.To.Add(to);

            using var client = new SmtpClient(host, port);
            if (!string.IsNullOrWhiteSpace(userName))
            {
                client.Credentials = new NetworkCredential(userName, password);
                client.EnableSsl = true;
            }

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Email sent to {To} with subject '{Subject}'.", to, subject);
        }
        catch (Exception ex)
        {
            // Best-effort by design: an email failure must never fail the business operation.
            _logger.LogError(ex, "Failed to send email to {To} with subject '{Subject}'.", to, subject);
        }
    }
}
