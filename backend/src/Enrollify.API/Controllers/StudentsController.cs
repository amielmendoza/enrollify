using Enrollify.Application.DTOs.Students;
using Enrollify.Application.Features.Students.Commands;
using Enrollify.Application.Features.Students.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Enrollify.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly ISender _sender;

    public StudentsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _sender.Send(new GetStudentsQuery(search, page, pageSize));
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _sender.Send(new GetStudentByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest request)
    {
        var result = await _sender.Send(new CreateStudentCommand(
            request.LRN, request.FirstName, request.MiddleName, request.LastName,
            request.BirthDate, request.Gender, request.Address,
            request.ContactNumber, request.Email, request.GuardianName, request.GuardianContact));
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentRequest request)
    {
        var result = await _sender.Send(new UpdateStudentCommand(
            id, request.LRN, request.FirstName, request.MiddleName, request.LastName,
            request.BirthDate, request.Gender, request.Address,
            request.ContactNumber, request.Email, request.GuardianName, request.GuardianContact));
        return Ok(result);
    }

    [HttpPost("{id:guid}/create-account")]
    [Authorize(Roles = "Admin,Registrar")]
    public async Task<IActionResult> CreateAccount(Guid id, [FromBody] CreateStudentAccountRequest request)
    {
        var userId = await _sender.Send(new CreateStudentAccountCommand(id, request.Email, request.Password));
        return Ok(new { userId });
    }

    [HttpGet("me")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var result = await _sender.Send(new GetMyProfileQuery(userId));
        return Ok(result);
    }
}
