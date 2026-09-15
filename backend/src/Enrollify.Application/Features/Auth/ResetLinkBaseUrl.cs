namespace Enrollify.Application.Features.Auth;

/// <summary>
/// Chooses the base URL that password-reset emails link to (CWE-640 reset-link-poisoning
/// guard). The configured frontend base URL always wins. A caller-supplied Origin header is
/// attacker-controlled on an anonymous endpoint (any curl can set it; CORS never applies to
/// the email the server sends), so it is trusted ONLY when it exactly matches one of the
/// configured allowed origins — otherwise the API's own host is the last resort.
/// </summary>
public static class ResetLinkBaseUrl
{
    public static string Resolve(string? configuredBaseUrl, string? requestOrigin, IEnumerable<string>? allowedOrigins, string apiBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
            return configuredBaseUrl;

        if (!string.IsNullOrWhiteSpace(requestOrigin) && allowedOrigins != null)
        {
            // Exact origin match (scheme + host + port); case-insensitive per RFC 6454 and
            // tolerant of a trailing slash in config — nothing looser (no prefixes, no subdomains).
            var normalizedOrigin = requestOrigin.TrimEnd('/');
            if (allowedOrigins.Any(o => !string.IsNullOrWhiteSpace(o)
                    && string.Equals(o.TrimEnd('/'), normalizedOrigin, StringComparison.OrdinalIgnoreCase)))
                return requestOrigin;
        }

        return apiBaseUrl;
    }
}
