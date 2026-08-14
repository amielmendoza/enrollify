using Enrollify.Application.DTOs.Tenants;
using Enrollify.Application.Features.Registrars;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

/// <summary>
/// Registrar accounts for the current Admin's tenant. Admins create and manage
/// Registrars here; SuperAdmins do not — they only manage Admins.
/// </summary>
[ApiController]
[Route("api/registrars")]
[Authorize(Roles = "Admin")]
public class RegistrarsController : ControllerBase
{
    private readonly ISender _sender;

    public RegistrarsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _sender.Send(new GetMyRegistrarsQuery());
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRegistrarRequest request)
    {
        var result = await _sender.Send(new CreateRegistrarCommand(
            request.Email, request.FirstName, request.LastName, request.Password));
        return Ok(result);
    }

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Update(Guid userId, [FromBody] UpdateRegistrarRequest request)
    {
        var result = await _sender.Send(new UpdateRegistrarCommand(
            userId, request.FirstName, request.LastName, request.IsActive));
        return Ok(result);
    }

    [HttpPost("{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId, [FromBody] ResetRegistrarPasswordRequest request)
    {
        await _sender.Send(new ResetRegistrarPasswordCommand(userId, request.NewPassword));
        return Ok(new { success = true });
    }
}
