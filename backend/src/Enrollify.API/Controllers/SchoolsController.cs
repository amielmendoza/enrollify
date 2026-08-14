using Enrollify.Application.DTOs.Admissions;
using Enrollify.Application.Features.Admissions.Commands;
using Enrollify.Application.Features.Admissions.Queries;
using Enrollify.Application.Features.ApplicationFormFields.Queries;
using Enrollify.Application.Features.SchoolYears.Queries;
using Enrollify.Application.Features.Tenants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

/// <summary>
/// Public, slug-based endpoints for the per-school /apply experience.
/// Anonymous applicants land here from /tenants/{slug}/apply on the SPA.
/// All endpoints resolve the tenant from the {slug} segment, not from headers, so callers
/// don't need to know the school's GUID.
/// </summary>
[ApiController]
[Route("api/schools/{slug}")]
[AllowAnonymous]
public class SchoolsController : ControllerBase
{
    private readonly ISender _sender;

    public SchoolsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetSchool(string slug)
    {
        var result = await _sender.Send(new GetTenantBySlugQuery(slug));
        return Ok(result);
    }

    [HttpGet("form-fields")]
    public async Task<IActionResult> GetFormFields(string slug)
    {
        var school = await _sender.Send(new GetTenantBySlugQuery(slug));
        var fields = await _sender.Send(new GetApplicationFormFieldsQuery(school.Id));
        return Ok(fields.Where(f => f.IsVisible).ToList());
    }

    [HttpGet("school-years")]
    public async Task<IActionResult> GetSchoolYears(string slug)
    {
        var school = await _sender.Send(new GetTenantBySlugQuery(slug));
        var result = await _sender.Send(new GetSchoolYearsByTenantQuery(school.Id));
        return Ok(result);
    }

    [HttpGet("applications/{applicationNumber}/status")]
    public async Task<IActionResult> GetApplicationStatus(string slug, string applicationNumber)
    {
        var school = await _sender.Send(new GetTenantBySlugQuery(slug));
        var result = await _sender.Send(new GetApplicationStatusQuery(school.Id, applicationNumber));
        return Ok(result);
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply(string slug, [FromBody] SubmitApplicationRequest request)
    {
        var school = await _sender.Send(new GetTenantBySlugQuery(slug));

        // Authenticated parent adding another child? Mirror the behavior of /api/admissions/apply.
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
            school.Id));
        return Ok(result);
    }
}
