using Enrollify.Application.Features.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Registrar")]
public class DashboardController : ControllerBase
{
    private readonly ISender _sender;

    public DashboardController(ISender sender) => _sender = sender;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _sender.Send(new GetDashboardStatsQuery());
        return Ok(result);
    }
}
