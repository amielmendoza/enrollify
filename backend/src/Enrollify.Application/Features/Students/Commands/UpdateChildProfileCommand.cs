using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

public record UpdateChildProfileCommand(
    Guid StudentId,
    Guid ParentUserId,
    string? ContactNumber,
    string? Email,
    string? Address,
    string? GuardianName,
    string? GuardianContact) : IRequest<StudentDto>;

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

        await _context.SaveChangesAsync(cancellationToken);

        return new StudentDto(student.Id, student.LRN, student.FirstName, student.MiddleName, student.LastName,
            student.BirthDate, student.Gender, student.Address, student.ContactNumber, student.Email,
            student.GuardianName, student.GuardianContact, student.FullName);
    }
}
