using Enrollify.Application.DTOs.SchoolYears;
using Enrollify.Application.Features.SchoolYears.Commands;
using Enrollify.Application.Features.SchoolYears.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchoolYearsController : ControllerBase
{
    private readonly ISender _sender;

    public SchoolYearsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll()
    {
        var result = await _sender.Send(new GetSchoolYearsQuery());
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateSchoolYearRequest request)
    {
        var result = await _sender.Send(new CreateSchoolYearCommand(request.Name, request.StartDate, request.EndDate));
        return Ok(result);
    }

    [HttpPost("{id:guid}/set-active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetActive(Guid id)
    {
        var result = await _sender.Send(new SetActiveSchoolYearCommand(id));
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteSchoolYearCommand(id));
        return NoContent();
    }
}
