using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EnrollmentsController : ControllerBase
{
    private readonly ISender _sender;

    public EnrollmentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? schoolYear, [FromQuery] string? gradeLevel,
        [FromQuery] EnrollmentStatus? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(new GetEnrollmentsQuery(schoolYear, gradeLevel, status, search, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _sender.Send(new GetEnrollmentByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEnrollmentRequest request)
    {
        var result = await _sender.Send(new CreateEnrollmentCommand(request.StudentId, request.SchoolYear, request.GradeLevel));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/move-step")]
    public async Task<IActionResult> MoveStep(Guid id, [FromBody] MoveEnrollmentStepRequest request)
    {
        var result = await _sender.Send(new MoveEnrollmentStepCommand(id, request.Remarks));
        return Ok(result);
    }

    [HttpPost("{id:guid}/assign-section")]
    public async Task<IActionResult> AssignSection(Guid id, [FromBody] AssignSectionRequest request)
    {
        var result = await _sender.Send(new AssignSectionCommand(id, request.SectionId));
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyEnrollments()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new GetMyEnrollmentsQuery(userId));
        return Ok(result);
    }

    [HttpPost("request")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> RequestEnrollment()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new RequestEnrollmentCommand(userId));
        return Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitEnrollment(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new SubmitEnrollmentCommand(id, userId));
        return Ok(result);
    }

    [HttpPost("requirements/{requirementId:guid}/upload")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> UploadRequirement(Guid requirementId, [FromBody] SubmitRequirementRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _sender.Send(new UploadRequirementCommand(requirementId, userId, request.FileName));
        return Ok(new { success = true });
    }

    [HttpPost("payment-plan")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SelectPaymentPlan([FromBody] SelectPaymentPlanRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _sender.Send(new SelectPaymentPlanCommand(userId, request.PaymentPlan));
        return Ok(new { success = true });
    }
}
