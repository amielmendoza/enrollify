using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.DTOs.Students;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

/// <summary>Student edits their own contact details and home address (null = unchanged).</summary>
public record UpdateMyProfileCommand(
    Guid UserId,
    string? ContactNumber,
    string? Email,
    string? Address,
    string? GuardianName,
    string? GuardianContact,
    PsgcAddressDto? HomeAddress = null,
    string? FatherContactNumber = null,
    string? MotherContactNumber = null,
    string? GuardianRelationship = null) : IRequest<StudentDto>;

public class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        RuleFor(x => x.ContactNumber).MaximumLength(50);
        RuleFor(x => x.Email).MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.GuardianName).MaximumLength(200);
        RuleFor(x => x.GuardianContact).MaximumLength(50);
        RuleFor(x => x.GuardianRelationship).MaximumLength(50);
        RuleFor(x => x.FatherContactNumber).MaximumLength(50);
        RuleFor(x => x.MotherContactNumber).MaximumLength(50);
        RuleFor(x => x.HomeAddress!).SetValidator(new PsgcAddressDtoValidator()).When(x => x.HomeAddress != null);
    }
}

public class UpdateMyProfileCommandHandler : IRequestHandler<UpdateMyProfileCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateMyProfileCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<StudentDto> Handle(UpdateMyProfileCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.UserId == request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException("Student not found.");

        if (request.ContactNumber != null) student.ContactNumber = request.ContactNumber;
        if (request.Email != null) student.Email = request.Email;
        if (request.Address != null) student.Address = request.Address;
        if (request.GuardianName != null) student.GuardianName = request.GuardianName;
        if (request.GuardianContact != null) student.GuardianContact = request.GuardianContact;
        // The user's own edit: set directly (blank clears), not the blank-only guard used by approval.
        if (request.GuardianRelationship != null) student.GuardianRelationship = LearnerProfileMapping.Clean(request.GuardianRelationship);
        if (request.FatherContactNumber != null) student.FatherContactNumber = LearnerProfileMapping.Clean(request.FatherContactNumber);
        if (request.MotherContactNumber != null) student.MotherContactNumber = LearnerProfileMapping.Clean(request.MotherContactNumber);
        if (request.HomeAddress != null) LearnerProfileMapping.ApplyAddress(request.HomeAddress, student.HomeAddress);

        await _context.SaveChangesAsync(cancellationToken);

        return StudentMapping.ToDto(student);
    }
}
