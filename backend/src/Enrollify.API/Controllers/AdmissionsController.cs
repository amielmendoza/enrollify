using Enrollify.Application.DTOs.Admissions;
using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Application.Features.Admissions.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdmissionsController : ControllerBase
{
    private readonly ISender _sender;

    public AdmissionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpPost("apply")]
    [AllowAnonymous]
    public async Task<IActionResult> Apply([FromBody] SubmitApplicationRequest request, [FromHeader(Name = "X-Tenant-Id")] Guid tenantId)
    {
        Guid? authenticatedParentUserId = null;
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Parent"))
        {
            authenticatedParentUserId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        }

        var applicants = (request.Applicants ?? new List<ApplicantData>())
            .Select(a => new SubmitApplicationCommand.Applicant(
                a.FirstName, a.MiddleName, a.LastName,
                a.Email, a.ContactNumber, a.Gender,
                a.DateOfBirth, a.Address,
                a.GradeLevel, a.SchoolYear,
                a.PreviousSchool, a.PreviousSchoolAddress,
                a.GuardianName, a.GuardianContact, a.GuardianRelationship,
                a.CustomFieldValues))
            .ToList();

        var result = await _sender.Send(new SubmitApplicationCommand(
            authenticatedParentUserId,
            request.ApplicationType,
            request.ParentFirstName, request.ParentLastName,
            request.ParentEmail, request.ParentContactNumber,
            applicants,
            tenantId));
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? studentId = null)
    {
        var result = await _sender.Send(new GetApplicationsQuery(status, search, page, pageSize, studentId));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _sender.Send(new GetApplicationByIdQuery(id));
        return Ok(result);
    }

    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewApplicationRequest request)
    {
        var result = await _sender.Send(new ReviewApplicationCommand(id, request.IsApproved, request.Notes, CurrentActor()));
        return Ok(result);
    }

    [HttpPost("{id:guid}/under-review")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> MarkUnderReview(Guid id)
    {
        await _sender.Send(new SetApplicationUnderReviewCommand(id, CurrentActor()));
        return Ok(new { success = true });
    }

    // Who is acting, for ReviewedBy: display name, else email.
    private string CurrentActor() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        ?? "Staff";
}
