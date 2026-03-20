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

    public string FullName => $"{FirstName} {LastName}";
}
