using Enrollify.Application.DTOs.ApplicationFormFields;
using Enrollify.Application.Features.ApplicationFormFields.Commands;
using Enrollify.Application.Features.ApplicationFormFields.Queries;
using Enrollify.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/applicationformfields")]
public class ApplicationFormFieldsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ITenantProvider _tenantProvider;

    public ApplicationFormFieldsController(ISender sender, ITenantProvider tenantProvider)
    {
        _sender = sender;
        _tenantProvider = tenantProvider;
    }

    /// <summary>Admin view — returns every field for the current tenant.</summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _sender.Send(new GetApplicationFormFieldsQuery(_tenantProvider.GetTenantId()));
        return Ok(result);
    }

    /// <summary>
    /// Public — returns only the visible fields for the tenant, used by the /apply form
    /// before the user has logged in.
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic([FromHeader(Name = "X-Tenant-Id")] Guid tenantId)
    {
        var result = await _sender.Send(new GetApplicationFormFieldsQuery(tenantId));
        return Ok(result.Where(f => f.IsVisible).ToList());
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateApplicationFormFieldRequest request)
    {
        var result = await _sender.Send(new CreateApplicationFormFieldCommand(
            request.FieldKey, request.Label, request.FieldType, request.Section, request.AppliesTo,
            request.IsRequired, request.IsVisible, request.DisplayOrder, request.Options, request.HelpText));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateApplicationFormFieldRequest request)
    {
        var result = await _sender.Send(new UpdateApplicationFormFieldCommand(
            id, request.FieldKey, request.Label, request.FieldType, request.Section, request.AppliesTo,
            request.IsRequired, request.IsVisible, request.DisplayOrder, request.Options, request.HelpText));
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteApplicationFormFieldCommand(id));
        return NoContent();
    }

    /// <summary>
    /// Idempotently restores any missing default form fields for the current tenant.
    /// Custom fields are preserved. Returns the number of fields that were inserted.
    /// </summary>
    [HttpPost("restore-defaults")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RestoreDefaults()
    {
        var added = await _sender.Send(new RestoreDefaultApplicationFormFieldsCommand());
        return Ok(new { added });
    }
}
