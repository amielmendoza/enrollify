namespace Enrollify.Application.DTOs.Tenants;

public record TenantUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>SuperAdmin always creates Admin users via this DTO; the Role field is implicitly "Admin".</summary>
public record CreateTenantUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password);

public record UpdateTenantUserRequest(
    string FirstName,
    string LastName,
    bool IsActive);

public record ResetTenantUserPasswordRequest(string NewPassword);
