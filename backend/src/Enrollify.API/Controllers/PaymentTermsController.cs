using Enrollify.Application.DTOs.PaymentTerms;
using Enrollify.Application.Features.PaymentTerms.Commands;
using Enrollify.Application.Features.PaymentTerms.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentTermsController : ControllerBase
{
    private readonly ISender _sender;

    public PaymentTermsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? schoolYear)
    {
        var result = await _sender.Send(new GetPaymentTermsQuery(schoolYear));
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save([FromBody] CreatePaymentTermRequest request)
    {
        var result = await _sender.Send(new SavePaymentTermCommand(
            request.SchoolYear, request.PlanType,
            request.DownPaymentPercent, request.InterestRatePercent,
            request.DiscountPercent, request.InstallmentCount));
        return Ok(result);
    }
}
