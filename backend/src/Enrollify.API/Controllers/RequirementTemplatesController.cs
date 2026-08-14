using Enrollify.Application.DTOs.RequirementTemplates;
using Enrollify.Application.Features.RequirementTemplates.Commands;
using Enrollify.Application.Features.RequirementTemplates.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RequirementTemplatesController : ControllerBase
{
    private readonly ISender _sender;

    public RequirementTemplatesController(ISender sender) => _sender = sender;

    // Registrars need to read templates while seeding new enrollments and reviewing requirements,
    // so list access stays Admin,Registrar. Mutations are Admin-only.
    [HttpGet]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _sender.Send(new GetRequirementTemplatesQuery());
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateRequirementTemplateRequest request)
    {
        var result = await _sender.Send(new CreateRequirementTemplateCommand(request.DocumentName, request.GradeLevel, request.DisplayOrder));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRequirementTemplateRequest request)
    {
        var result = await _sender.Send(new UpdateRequirementTemplateCommand(id, request.DocumentName, request.GradeLevel, request.IsActive, request.DisplayOrder));
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteRequirementTemplateCommand(id));
        return Ok(new { success = true });
    }
}
