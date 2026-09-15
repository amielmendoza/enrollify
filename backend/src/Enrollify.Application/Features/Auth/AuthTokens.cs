using System.Security.Cryptography;
using System.Text;
using Enrollify.Domain.Entities;

namespace Enrollify.Application.Features.Auth;

/// <summary>
/// Token plumbing shared by login, refresh, and the password-reset flow. Raw tokens are
/// cryptographically random and URL-safe; only their SHA-256 hex hash is ever persisted.
/// </summary>
public static class AuthTokens
{
    public const int RefreshTokenLifetimeDays = 14;
    public static readonly TimeSpan PasswordResetTokenLifetime = TimeSpan.FromHours(1);

    public static string GenerateUrlSafeToken(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>Hex SHA-256 (64 chars — fits the 200-char hash columns).</summary>
    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Builds a refresh-token row plus the raw value that is returned exactly once.</summary>
    public static (RefreshToken Entity, string Raw) IssueRefreshToken(Guid userId)
    {
        var raw = GenerateUrlSafeToken(64);
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = Sha256(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(RefreshTokenLifetimeDays)
        };
        return (entity, raw);
    }
}
