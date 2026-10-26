using Enrollify.Application.DTOs.SchoolProfile;
using Enrollify.Application.Features.SchoolProfile.Commands;
using Enrollify.Application.Features.SchoolProfile.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/school-profile")]
[Authorize]
public class SchoolProfileController : ControllerBase
{
    private readonly ISender _sender;

    public SchoolProfileController(ISender sender) => _sender = sender;

    [HttpGet]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> Get()
    {
        var result = await _sender.Send(new GetSchoolProfileQuery(CurrentTenantId()));
        return Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update([FromBody] UpdateSchoolProfileRequest request)
    {
        var result = await _sender.Send(new UpdateSchoolProfileCommand(
            CurrentTenantId(),
            request.DepEdSchoolId, request.DepEdRegionCode, request.DepEdRegionName,
            request.DepEdDivision, request.DepEdDistrict, request.SchoolHeadName,
            UpdatedBy()));
        return Ok(result);
    }

    // Tenant is not a TenantEntity (no global query filter), so the handlers select the
    // current school's row by id; the id comes from the signed-in user's JWT.
    private Guid CurrentTenantId() => Guid.Parse(User.FindFirst("TenantId")!.Value);

    private string UpdatedBy() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
        ?? "Admin";
}
