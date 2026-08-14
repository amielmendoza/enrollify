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

    public AuthController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _sender.Send(new LoginCommand(request.Email, request.Password));
        return Ok(result);
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
