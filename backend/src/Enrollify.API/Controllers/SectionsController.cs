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
    public async Task<IActionResult> GetAll([FromQuery] string? schoolYear, [FromQuery] string? gradeLevel)
    {
        var result = await _sender.Send(new GetSectionsQuery(schoolYear, gradeLevel));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Create([FromBody] CreateSectionRequest request)
    {
        var result = await _sender.Send(new CreateSectionCommand(
            request.Name, request.GradeLevel, request.SchoolYear, request.Capacity, request.Adviser));
        return Ok(result);
    }
}
