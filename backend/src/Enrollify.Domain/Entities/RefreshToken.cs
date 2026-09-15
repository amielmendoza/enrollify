using Enrollify.Domain.Common;

namespace Enrollify.Domain.Entities;

/// <summary>
/// A refresh token issued at login. Deliberately a BaseEntity, NOT a TenantEntity:
/// auth endpoints are tenant-exempt and the token must resolve to its user before any
/// tenant context exists, so it must not sit behind the global tenant query filter.
/// Only the hash is stored, never the raw token. Revocation is a soft mark (RevokedAt)
/// so a replayed revoked token is detectable.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public string TokenHash { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}
