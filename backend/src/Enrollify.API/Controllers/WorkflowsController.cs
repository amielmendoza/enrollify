using Enrollify.Application.DTOs.Workflows;
using Enrollify.Application.Features.Workflows.Commands;
using Enrollify.Application.Features.Workflows.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class WorkflowsController : ControllerBase
{
    private readonly ISender _sender;

    public WorkflowsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _sender.Send(new GetWorkflowsQuery());
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRequest request)
    {
        var result = await _sender.Send(new CreateWorkflowCommand(request.Name, request.Description, request.Steps));
        return Ok(result);
    }
}
