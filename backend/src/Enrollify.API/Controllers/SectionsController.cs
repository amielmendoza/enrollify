using Enrollify.Application.DTOs.Sections;
using Enrollify.Application.Features.Sections.Commands;
using Enrollify.Application.Features.Sections.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SectionsController : ControllerBase
{
    private readonly ISender _sender;

    public SectionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? schoolYear, [FromQuery] string? gradeLevel, [FromQuery] bool includeInactive = false)
    {
        var result = await _sender.Send(new GetSectionsQuery(schoolYear, gradeLevel, includeInactive));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateSectionRequest request)
    {
        var result = await _sender.Send(new CreateSectionCommand(
            request.Name, request.GradeLevel, request.SchoolYear, request.Capacity, request.Adviser));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSectionRequest request)
    {
        var result = await _sender.Send(new UpdateSectionCommand(
            id, request.Name, request.GradeLevel, request.SchoolYear, request.Capacity, request.Adviser, request.IsActive));
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var context = HttpContext.RequestServices.GetRequiredService<Enrollify.Application.Common.Interfaces.IApplicationDbContext>();
        var section = await context.Sections.FindAsync(id);
        if (section == null) return NotFound();
        context.Sections.Remove(section);
        await context.SaveChangesAsync();
        return NoContent();
    }
}
