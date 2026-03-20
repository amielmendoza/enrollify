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
    public async Task<IActionResult> GetAll([FromQuery] string? schoolYear, [FromQuery] string? gradeLevel)
    {
        var result = await _sender.Send(new GetFeesQuery(schoolYear, gradeLevel));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Create([FromBody] CreateFeeRequest request)
    {
        var result = await _sender.Send(new CreateFeeCommand(request.Name, request.Description, request.Amount, request.SchoolYear, request.GradeLevel));
        return Ok(result);
    }
}
