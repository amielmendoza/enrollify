using Enrollify.Application.DTOs.Payments;
using Enrollify.Application.Features.Payments.Commands;
using Enrollify.Application.Features.Payments.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly ISender _sender;

    public PaymentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet("enrollment/{enrollmentId:guid}")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetByEnrollment(Guid enrollmentId)
    {
        var result = await _sender.Send(new GetPaymentsByEnrollmentQuery(enrollmentId));
        return Ok(result);
    }

    [HttpGet("balance/{enrollmentId:guid}")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetBalance(Guid enrollmentId)
    {
        var result = await _sender.Send(new GetBalanceQuery(enrollmentId));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentRequest request)
    {
        var result = await _sender.Send(new CreatePaymentCommand(
            request.EnrollmentId, request.Amount, request.PaymentMethod, request.ReferenceNumber, request.Remarks,
            request.ReceiptFileName, request.ReceiptFileUrl));
        return Ok(result);
    }

    [HttpPost("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> StudentPay([FromBody] ParentPayRequest request)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new StudentPaymentCommand(
            userId, request.Amount, request.PaymentMethod, request.ReferenceNumber, request.Remarks,
            request.ReceiptFileName, request.ReceiptFileUrl));
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyPayments()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new GetMyPaymentsQuery(userId));
        return Ok(result);
    }

    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> ReviewPayment(Guid id, [FromBody] ReviewPaymentRequest request)
    {
        var reviewerName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "Admin";
        var result = await _sender.Send(new ReviewPaymentCommand(id, request.IsApproved, request.Notes, reviewerName));
        return Ok(result);
    }
}
