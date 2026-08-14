using Enrollify.Application.DTOs.Enrollments;
using Enrollify.Application.DTOs.Payments;
using Enrollify.Application.DTOs.Students;
using Enrollify.Application.Features.Enrollments.Commands;
using Enrollify.Application.Features.Enrollments.Queries;
using Enrollify.Application.Features.Parent.Queries;
using Enrollify.Application.Features.Payments.Commands;
using Enrollify.Application.Features.Payments.Queries;
using Enrollify.Application.Features.Students.Commands;
using Enrollify.Application.Features.Students.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/parent")]
[Authorize(Roles = "Parent")]
public class ParentController : ControllerBase
{
    private readonly ISender _sender;

    public ParentController(ISender sender)
    {
        _sender = sender;
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("children")]
    public async Task<IActionResult> GetMyChildren()
    {
        var result = await _sender.Send(new GetMyChildrenQuery(CurrentUserId()));
        return Ok(result);
    }

    [HttpGet("children/{studentId:guid}/profile")]
    public async Task<IActionResult> GetChildProfile(Guid studentId)
    {
        var result = await _sender.Send(new GetChildProfileQuery(studentId, CurrentUserId()));
        return Ok(result);
    }

    [HttpPut("children/{studentId:guid}/profile")]
    public async Task<IActionResult> UpdateChildProfile(Guid studentId, [FromBody] UpdateChildProfileRequest request)
    {
        var result = await _sender.Send(new UpdateChildProfileCommand(
            studentId, CurrentUserId(),
            request.ContactNumber, request.Email, request.Address,
            request.GuardianName, request.GuardianContact));
        return Ok(result);
    }

    [HttpGet("children/{studentId:guid}/enrollments")]
    public async Task<IActionResult> GetChildEnrollments(Guid studentId)
    {
        var result = await _sender.Send(new GetChildEnrollmentsQuery(studentId, CurrentUserId()));
        return Ok(result);
    }

    [HttpPost("children/{studentId:guid}/enrollments/request")]
    public async Task<IActionResult> RequestEnrollment(Guid studentId)
    {
        var result = await _sender.Send(new RequestEnrollmentCommand(studentId, CurrentUserId()));
        return Ok(result);
    }

    [HttpPost("children/{studentId:guid}/enrollments/{enrollmentId:guid}/submit")]
    public async Task<IActionResult> SubmitEnrollment(Guid studentId, Guid enrollmentId)
    {
        var result = await _sender.Send(new SubmitEnrollmentCommand(enrollmentId, studentId, CurrentUserId()));
        return Ok(result);
    }

    [HttpPost("children/{studentId:guid}/enrollments/payment-plan")]
    public async Task<IActionResult> SelectPaymentPlan(Guid studentId, [FromBody] SelectPaymentPlanRequest request)
    {
        await _sender.Send(new SelectPaymentPlanCommand(studentId, CurrentUserId(), request.PaymentPlan));
        return Ok(new { success = true });
    }

    [HttpPost("children/{studentId:guid}/requirements/{requirementId:guid}/upload")]
    public async Task<IActionResult> UploadRequirement(Guid studentId, Guid requirementId, [FromBody] SubmitRequirementRequest request)
    {
        await _sender.Send(new UploadRequirementCommand(requirementId, studentId, CurrentUserId(), request.FileName, request.FileUrl));
        return Ok(new { success = true });
    }

    [HttpGet("children/{studentId:guid}/payments")]
    public async Task<IActionResult> GetChildPayments(Guid studentId)
    {
        var result = await _sender.Send(new GetChildPaymentsQuery(studentId, CurrentUserId()));
        return Ok(result);
    }

    [HttpGet("children/{studentId:guid}/ledger")]
    public async Task<IActionResult> GetChildLedger(Guid studentId)
    {
        var result = await _sender.Send(new GetChildLedgerQuery(studentId, CurrentUserId()));
        return Ok(result);
    }

    [HttpPost("children/{studentId:guid}/payments")]
    public async Task<IActionResult> Pay(Guid studentId, [FromBody] ParentPayRequest request)
    {
        var result = await _sender.Send(new ParentPaymentCommand(
            studentId, CurrentUserId(),
            request.Amount, request.PaymentMethod, request.ReferenceNumber, request.Remarks,
            request.ReceiptFileName, request.ReceiptFileUrl));
        return Ok(result);
    }
}
