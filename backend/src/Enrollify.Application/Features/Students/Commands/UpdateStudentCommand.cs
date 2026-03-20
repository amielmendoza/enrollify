using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

public record UpdateStudentCommand(
    Guid Id, string LRN, string FirstName, string MiddleName, string LastName,
    DateTime BirthDate, string? Gender, string Address,
    string? ContactNumber, string? Email, string? GuardianName, string? GuardianContact
) : IRequest<StudentDto>;

public class UpdateStudentCommandValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.LRN).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
    }
}

public class UpdateStudentCommandHandler : IRequestHandler<UpdateStudentCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateStudentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StudentDto> Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Student with ID '{request.Id}' not found.");

        student.LRN = request.LRN;
        student.FirstName = request.FirstName;
        student.MiddleName = request.MiddleName;
        student.LastName = request.LastName;
        student.BirthDate = request.BirthDate;
        student.Gender = request.Gender;
        student.Address = request.Address;
        student.ContactNumber = request.ContactNumber;
        student.Email = request.Email;
        student.GuardianName = request.GuardianName;
        student.GuardianContact = request.GuardianContact;

        await _context.SaveChangesAsync(cancellationToken);

        return new StudentDto(student.Id, student.LRN, student.FirstName, student.MiddleName, student.LastName,
            student.BirthDate, student.Gender, student.Address, student.ContactNumber, student.Email,
            student.GuardianName, student.GuardianContact, student.FullName);
    }
}
