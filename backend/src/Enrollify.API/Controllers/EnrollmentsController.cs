using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Application.Features.Payments.Queries;
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
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? schoolYear, [FromQuery] string? gradeLevel,
        [FromQuery] EnrollmentStatus? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] bool pendingPaymentsOnly = false)
    {
        var result = await _sender.Send(new GetEnrollmentsQuery(schoolYear, gradeLevel, status, search, page, pageSize, pendingPaymentsOnly));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _sender.Send(new GetEnrollmentByIdQuery(id));
        return Ok(result);
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetHistory(Guid id)
    {
        var result = await _sender.Send(new GetEnrollmentHistoryQuery(id));
        return Ok(result);
    }

    [HttpGet("{id:guid}/fees")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetFees(Guid id)
    {
        var result = await _sender.Send(new GetEnrollmentFeesQuery(id));
        return Ok(result);
    }

    [HttpGet("{id:guid}/ledger")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetLedger(Guid id)
    {
        var result = await _sender.Send(new GetEnrollmentLedgerQuery(id));
        return Ok(result);
    }

    [HttpPost("{id:guid}/adjustments")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> PostAdjustment(Guid id, [FromBody] PostAdjustmentRequest request)
    {
        var postedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                       ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                       ?? "Admin";
        var result = await _sender.Send(new PostLedgerAdjustmentCommand(id, request.Type, request.Description, request.Amount, postedBy));
        return Ok(result);
    }

    [HttpPost("{id:guid}/adjustments/{adjustmentId:guid}/void")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> VoidAdjustment(Guid id, Guid adjustmentId, [FromBody] VoidAdjustmentRequest request)
    {
        var voidedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                       ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                       ?? "Admin";
        await _sender.Send(new VoidLedgerAdjustmentCommand(id, adjustmentId, request.Reason, voidedBy));
        return Ok(new { success = true });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Create([FromBody] CreateEnrollmentRequest request)
    {
        var result = await _sender.Send(new CreateEnrollmentCommand(request.StudentId, request.SchoolYear, request.GradeLevel));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/move-step")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> MoveStep(Guid id, [FromBody] MoveEnrollmentStepRequest request)
    {
        var result = await _sender.Send(new MoveEnrollmentStepCommand(id, request.Remarks));
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelEnrollmentRequest request)
    {
        var cancelledBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                          ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                          ?? "Admin";
        var result = await _sender.Send(new CancelEnrollmentCommand(id, request.Reason, cancelledBy));
        return Ok(result);
    }

    [HttpPost("{id:guid}/assign-section")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> AssignSection(Guid id, [FromBody] AssignSectionRequest request)
    {
        var result = await _sender.Send(new AssignSectionCommand(id, request.SectionId));
        return Ok(result);
    }

    // ----- Student self-service (only for users with Role = Student) -----

    [HttpGet("me")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyEnrollments()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new GetMyEnrollmentsQuery(userId));
        return Ok(result);
    }

    [HttpGet("me/ledger")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyLedger()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new GetMyLedgerQuery(userId));
        return Ok(result);
    }

    [HttpPost("request")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> RequestEnrollment()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new StudentRequestEnrollmentCommand(userId));
        return Ok(result);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitEnrollment(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new StudentSubmitEnrollmentCommand(id, userId));
        return Ok(result);
    }

    [HttpPost("requirements/{requirementId:guid}/upload")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> UploadRequirement(Guid requirementId, [FromBody] SubmitRequirementRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _sender.Send(new StudentUploadRequirementCommand(requirementId, userId, request.FileName, request.FileUrl));
        return Ok(new { success = true });
    }

    [HttpPost("payment-plan")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SelectPaymentPlan([FromBody] SelectPaymentPlanRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await _sender.Send(new StudentSelectPaymentPlanCommand(userId, request.PaymentPlan));
        return Ok(new { success = true });
    }

    // ----- Admin/Registrar requirement management -----

    [HttpPost("requirements/{requirementId:guid}/admin-upload")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> AdminUploadRequirement(Guid requirementId, [FromBody] AdminUploadRequirementRequest request)
    {
        await _sender.Send(new AdminUploadRequirementCommand(requirementId, request.FileName, request.FileUrl));
        return Ok(new { success = true });
    }

    [HttpPost("requirements/{requirementId:guid}/review")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> ReviewRequirement(Guid requirementId, [FromBody] ReviewRequirementRequest request)
    {
        var reviewer = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                       ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                       ?? "Admin";
        await _sender.Send(new ReviewRequirementCommand(requirementId, request.IsVerified, request.Notes, reviewer));
        return Ok(new { success = true });
    }
}
