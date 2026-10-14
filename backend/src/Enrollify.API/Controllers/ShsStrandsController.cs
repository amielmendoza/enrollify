using Enrollify.Application.DTOs.ShsStrands;
using Enrollify.Application.Features.ShsStrands.Commands;
using Enrollify.Application.Features.ShsStrands.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/shs-strands")]
[Authorize]
public class ShsStrandsController : ControllerBase
{
    private readonly ISender _sender;

    public ShsStrandsController(ISender sender) => _sender = sender;

    // Any signed-in role in the tenant: families need the active list to re-enroll into SHS.
    // Inactive rows are a staff concern only.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var staff = User.IsInRole("Admin") || User.IsInRole("Registrar");
        var result = await _sender.Send(new GetShsStrandsQuery(includeInactive && staff));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Create([FromBody] CreateShsStrandRequest request)
    {
        var result = await _sender.Send(new CreateShsStrandCommand(request.Track, request.Strand, request.DisplayOrder));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShsStrandRequest request)
    {
        var result = await _sender.Send(new UpdateShsStrandCommand(id, request.Track, request.Strand, request.IsActive, request.DisplayOrder));
        return Ok(result);
    }
}
