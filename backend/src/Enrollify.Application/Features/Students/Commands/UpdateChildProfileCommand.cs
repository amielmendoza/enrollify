using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.DTOs.Students;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

/// <summary>
/// Parent edits their child's contact details and home address. DepEd identity fields
/// (LRN, PSA no., IP/4Ps/disability, parents' names) are registrar-only — no parameters here.
/// Null means "unchanged" for every field.
/// </summary>
public record UpdateChildProfileCommand(
    Guid StudentId,
    Guid ParentUserId,
    string? ContactNumber,
    string? Email,
    string? Address,
    string? GuardianName,
    string? GuardianContact,
    PsgcAddressDto? HomeAddress = null,
    string? FatherContactNumber = null,
    string? MotherContactNumber = null,
    string? GuardianRelationship = null) : IRequest<StudentDto>;

public class UpdateChildProfileCommandValidator : AbstractValidator<UpdateChildProfileCommand>
{
    public UpdateChildProfileCommandValidator()
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

public class UpdateChildProfileCommandHandler : IRequestHandler<UpdateChildProfileCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateChildProfileCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<StudentDto> Handle(UpdateChildProfileCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.ParentUserId == request.ParentUserId, cancellationToken)
            ?? throw new KeyNotFoundException("Child not found or you do not have access to this student.");

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
