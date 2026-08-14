namespace Enrollify.Application.DTOs.Tenants;

/// <summary>
/// Registrar accounts within a single tenant — managed by that tenant's Admin.
/// Returned by /api/registrars and the matching create/update endpoints.
/// </summary>
public record RegistrarDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    DateTime CreatedAt);

public record CreateRegistrarRequest(
    string Email,
    string FirstName,
    string LastName,
    string Password);

public record UpdateRegistrarRequest(
    string FirstName,
    string LastName,
    bool IsActive);

public record ResetRegistrarPasswordRequest(string NewPassword);
