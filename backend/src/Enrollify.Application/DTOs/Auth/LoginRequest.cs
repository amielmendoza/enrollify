namespace Enrollify.Application.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record LoginResponse(
    string Token, string Email, string FullName, string Role, Guid TenantId,
    string? RefreshToken = null, bool MustChangePassword = false);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record RefreshRequest(string RefreshToken);

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string Role);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
