using Enrollify.Application.Features.Reports;
using Enrollify.Application.Features.Reports.DepEd;
using Enrollify.Application.Features.Reports.Queries;
using Enrollify.Domain.Enums;
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

    [HttpGet("enrollment-register")]
    public async Task<IActionResult> GetEnrollmentRegister(
        [FromQuery] string? schoolYear, [FromQuery] string? gradeLevel,
        [FromQuery] EnrollmentStatus? status, [FromQuery] Guid? sectionId,
        [FromQuery] bool includeCancelled = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var result = await _sender.Send(new GetEnrollmentRegisterQuery(
            new EnrollmentRegisterFilter(schoolYear, gradeLevel, status, sectionId, includeCancelled), page, pageSize));
        return Ok(result);
    }

    [HttpGet("enrollment-register/export")]
    public async Task<IActionResult> ExportEnrollmentRegister(
        [FromQuery] string? schoolYear, [FromQuery] string? gradeLevel,
        [FromQuery] EnrollmentStatus? status, [FromQuery] Guid? sectionId,
        [FromQuery] bool includeCancelled = false)
    {
        var rows = await _sender.Send(new GetEnrollmentRegisterExportQuery(
            new EnrollmentRegisterFilter(schoolYear, gradeLevel, status, sectionId, includeCancelled)));
        var csv = EnrollmentRegisterCsvBuilder.Build(rows);
        var fileName = $"enrollment-register_{schoolYear ?? "active-year"}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    [HttpGet("deped/lis-worklist")]
    public async Task<IActionResult> GetLisWorklist(
        [FromQuery] string? schoolYear, [FromQuery] Guid? sectionId, [FromQuery] bool noSection = false)
    {
        var tenantId = Guid.Parse(User.FindFirst("TenantId")!.Value);
        var result = await _sender.Send(new GetLisWorklistQuery(tenantId, schoolYear, sectionId, noSection));
        return Ok(result);
    }

    [HttpGet("deped/quick-count")]
    public async Task<IActionResult> GetQuickCount([FromQuery] string? schoolYear, [FromQuery] DateOnly? asOf)
    {
        var tenantId = Guid.Parse(User.FindFirst("TenantId")!.Value);
        var result = await _sender.Send(new GetQuickCountQuery(tenantId, schoolYear, asOf));
        return Ok(result);
    }

    [HttpGet("deped/quick-count/export")]
    public async Task<IActionResult> ExportQuickCount([FromQuery] string? schoolYear, [FromQuery] DateOnly? asOf)
    {
        var tenantId = Guid.Parse(User.FindFirst("TenantId")!.Value);
        var result = await _sender.Send(new GetQuickCountExportQuery(tenantId, schoolYear, asOf));
        var csv = QuickCountCsvBuilder.Build(result);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", $"quick-count_{result.AsOf:yyyyMMdd}.csv");
    }
}
