namespace Enrollify.Application.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, string Email, string FullName, string Role, Guid TenantId);

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role);
