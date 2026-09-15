using Enrollify.Application.DTOs.Auth;
using Enrollify.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly IConfiguration _configuration;

    public AuthController(ISender sender, IConfiguration configuration)
    {
        _sender = sender;
        _configuration = configuration;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _sender.Send(new LoginCommand(request.Email, request.Password));
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh([FromBody] RefreshRequest request)
    {
        var result = await _sender.Send(new RefreshTokenCommand(request.RefreshToken));
        return Ok(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        // Reset links point at the SPA. "Frontend:BaseUrl" (env: Frontend__BaseUrl) wins —
        // and is REQUIRED in Production (fail-fast at startup). The Origin fallback is only
        // honored when it matches the CORS AllowedOrigins list: Origin is attacker-controlled
        // on an anonymous endpoint, and a poisoned base URL would put a live reset token in a
        // genuine email pointing at an attacker's domain (CWE-640).
        var baseUrl = Enrollify.Application.Features.Auth.ResetLinkBaseUrl.Resolve(
            _configuration["Frontend:BaseUrl"],
            Request.Headers.Origin.FirstOrDefault(),
            _configuration.GetSection("AllowedOrigins").Get<string[]>(),
            $"{Request.Scheme}://{Request.Host}");

        await _sender.Send(new ForgotPasswordCommand(request.Email, baseUrl));
        return Ok(new { success = true }); // always — no account enumeration
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _sender.Send(new ResetPasswordCommand(request.Email, request.Token, request.NewPassword));
        return Ok(new { success = true });
    }

    // Self-service signup does not exist: parent/student accounts are created on application
    // approval, registrars via /api/registrars, tenant admins via /api/tenants/{id}/users.
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, [FromHeader(Name = "X-Tenant-Id")] Guid tenantId)
    {
        var result = await _sender.Send(new RegisterCommand(
            request.Email, request.Password, request.FirstName, request.LastName, request.Role, tenantId));
        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _sender.Send(new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword));
        return Ok(new { success = true });
    }
}
