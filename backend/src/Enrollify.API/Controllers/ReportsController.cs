using Enrollify.Application.Features.Reports;
using Enrollify.Application.Features.Reports.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,Registrar")]
public class ReportsController : ControllerBase
{
    private readonly ISender _sender;

    public ReportsController(ISender sender) => _sender = sender;

    [HttpGet("collections")]
    public async Task<IActionResult> GetCollections(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? method,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _sender.Send(new GetCollectionsQuery(from, to, method, page, pageSize));
        return Ok(result);
    }

    [HttpGet("collections/export")]
    public async Task<IActionResult> ExportCollections(
        [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? method)
    {
        var rows = await _sender.Send(new GetCollectionsExportQuery(from, to, method));
        var csv = CollectionsCsvBuilder.Build(rows);
        var fileName = $"collections_{from:yyyyMMdd}_{to:yyyyMMdd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }
}
