namespace Enrollify.Application.DTOs.Tenants;

public record TenantDto(
    Guid Id,
    string Name,
    string Subdomain,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    bool IsActive,
    DateTime CreatedAt);

/// <summary>Slim DTO returned by the public lookup endpoints — no contact info.</summary>
public record PublicTenantDto(
    Guid Id,
    string Name,
    string Subdomain);

public record CreateTenantRequest(
    string Name,
    string Subdomain,
    string? ContactEmail,
    string? ContactPhone,
    string? Address);

public record UpdateTenantRequest(
    string Name,
    string Subdomain,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    bool IsActive);
