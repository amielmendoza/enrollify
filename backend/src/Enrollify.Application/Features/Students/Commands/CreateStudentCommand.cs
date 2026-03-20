using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

public record CreateStudentCommand(
    string LRN, string FirstName, string MiddleName, string LastName,
    DateTime BirthDate, string? Gender, string Address,
    string? ContactNumber, string? Email, string? GuardianName, string? GuardianContact
) : IRequest<StudentDto>;

public class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.LRN).NotEmpty().MaximumLength(20);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.BirthDate).NotEmpty().LessThan(DateTime.UtcNow);
        RuleFor(x => x.Address).NotEmpty().MaximumLength(500);
    }
}

public class CreateStudentCommandHandler : IRequestHandler<CreateStudentCommand, StudentDto>
{
    private readonly IApplicationDbContext _context;

    public CreateStudentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StudentDto> Handle(CreateStudentCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.Students.AnyAsync(s => s.LRN == request.LRN, cancellationToken);
        if (exists)
            throw new InvalidOperationException($"A student with LRN '{request.LRN}' already exists.");

        var student = new Student
        {
            LRN = request.LRN,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            Address = request.Address,
            ContactNumber = request.ContactNumber,
            Email = request.Email,
            GuardianName = request.GuardianName,
            GuardianContact = request.GuardianContact
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync(cancellationToken);

        return new StudentDto(student.Id, student.LRN, student.FirstName, student.MiddleName, student.LastName,
            student.BirthDate, student.Gender, student.Address, student.ContactNumber, student.Email,
            student.GuardianName, student.GuardianContact, student.FullName);
    }
}
