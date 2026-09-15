using Enrollify.Domain.Common;
using Enrollify.Domain.Enums;

namespace Enrollify.Domain.Entities;

public class User : TenantEntity
{
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Forces a password change on next login (e.g. after an admin-issued temporary password).</summary>
    public bool MustChangePassword { get; set; }

    // Single active password-reset token per user (deliberate design): issuing a new
    // token overwrites the previous one. Only the hash is stored, never the raw token.
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
