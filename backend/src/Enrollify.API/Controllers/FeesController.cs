using Enrollify.Application.DTOs.Fees;
using Enrollify.Application.Features.Fees.Commands;
using Enrollify.Application.Features.Fees.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeesController : ControllerBase
{
    private readonly ISender _sender;

    public FeesController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? schoolYear, [FromQuery] string? gradeLevel, [FromQuery] bool includeInactive = false)
    {
        var result = await _sender.Send(new GetFeesQuery(schoolYear, gradeLevel, includeInactive));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateFeeRequest request)
    {
        var result = await _sender.Send(new CreateFeeCommand(request.Name, request.Description, request.Amount, request.SchoolYear, request.GradeLevel));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFeeRequest request)
    {
        var result = await _sender.Send(new UpdateFeeCommand(
            id, request.Name, request.Description, request.Amount, request.SchoolYear, request.GradeLevel, request.IsActive));
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _sender.Send(new DeleteFeeCommand(id));
        return NoContent();
    }
}
