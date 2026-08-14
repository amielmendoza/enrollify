using Enrollify.Application.DTOs.Tenants;
using Enrollify.Application.Features.Tenants.Commands;
using Enrollify.Application.Features.Tenants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

/// <summary>
/// Schools (tenants). The platform-level entity that owns all per-school data.
/// Public endpoints power the /tenants directory and the per-tenant /apply page.
/// SuperAdmin endpoints manage the list of schools.
/// </summary>
[ApiController]
[Route("api/tenants")]
public class TenantsController : ControllerBase
{
    private readonly ISender _sender;

    public TenantsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive()
    {
        var result = await _sender.Send(new GetPublicActiveTenantsQuery());
        return Ok(result);
    }

    [HttpGet("public/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic(Guid id)
    {
        var result = await _sender.Send(new GetPublicTenantQuery(id));
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = false)
    {
        var result = await _sender.Send(new GetTenantsQuery(activeOnly));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateTenantRequest request)
    {
        var result = await _sender.Send(new CreateTenantCommand(
            request.Name, request.Subdomain, request.ContactEmail, request.ContactPhone, request.Address));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantRequest request)
    {
        var result = await _sender.Send(new UpdateTenantCommand(
            id, request.Name, request.Subdomain, request.ContactEmail, request.ContactPhone, request.Address, request.IsActive));
        return Ok(result);
    }

    // ----- Per-tenant user management (SuperAdmin scoped per tenant) -----

    [HttpGet("{tenantId:guid}/users")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetUsers(Guid tenantId)
    {
        var result = await _sender.Send(new GetTenantUsersQuery(tenantId));
        return Ok(result);
    }

    [HttpPost("{tenantId:guid}/users")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> CreateUser(Guid tenantId, [FromBody] CreateTenantUserRequest request)
    {
        // SuperAdmin only ever creates Admins through this endpoint; the role is enforced server-side.
        var result = await _sender.Send(new CreateTenantUserCommand(
            tenantId, request.Email, request.FirstName, request.LastName, "Admin", request.Password));
        return Ok(result);
    }

    [HttpPut("{tenantId:guid}/users/{userId:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> UpdateUser(Guid tenantId, Guid userId, [FromBody] UpdateTenantUserRequest request)
    {
        var result = await _sender.Send(new UpdateTenantUserCommand(
            tenantId, userId, request.FirstName, request.LastName, request.IsActive));
        return Ok(result);
    }

    [HttpPost("{tenantId:guid}/users/{userId:guid}/reset-password")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ResetPassword(Guid tenantId, Guid userId, [FromBody] ResetTenantUserPasswordRequest request)
    {
        await _sender.Send(new ResetTenantUserPasswordCommand(tenantId, userId, request.NewPassword));
        return Ok(new { success = true });
    }
}
