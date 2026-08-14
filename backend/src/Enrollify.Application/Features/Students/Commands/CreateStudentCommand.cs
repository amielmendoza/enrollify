using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Students;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

public record CreateStudentCommand(
    string? LRN, string FirstName, string MiddleName, string LastName,
    DateTime BirthDate, string? Gender, string Address,
    string? ContactNumber, string? Email, string? GuardianName, string? GuardianContact,
    Guid? UserId = null,        // Set for Student-mode applications (student logs in as themselves)
    Guid? ParentUserId = null   // Set for Parent-mode applications (parent owns this student)
) : IRequest<StudentDto>;

public class CreateStudentCommandValidator : AbstractValidator<CreateStudentCommand>
{
    public CreateStudentCommandValidator()
    {
        RuleFor(x => x.LRN).MaximumLength(20);
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
        var lrn = string.IsNullOrWhiteSpace(request.LRN) ? null : request.LRN.Trim();

        if (lrn != null)
        {
            var exists = await _context.Students.AnyAsync(s => s.LRN == lrn, cancellationToken);
            if (exists)
                throw new InvalidOperationException($"A student with LRN '{lrn}' already exists.");
        }
        else
        {
            lrn = await NextNumericLrnAsync(cancellationToken);
        }

        var student = new Student
        {
            LRN = lrn,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            BirthDate = request.BirthDate,
            Gender = request.Gender,
            Address = request.Address,
            ContactNumber = request.ContactNumber,
            Email = request.Email,
            GuardianName = request.GuardianName,
            GuardianContact = request.GuardianContact,
            UserId = request.UserId,
            ParentUserId = request.ParentUserId
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync(cancellationToken);

        return new StudentDto(student.Id, student.LRN, student.FirstName, student.MiddleName, student.LastName,
            student.BirthDate, student.Gender, student.Address, student.ContactNumber, student.Email,
            student.GuardianName, student.GuardianContact, student.FullName);
    }

    private async Task<string> NextNumericLrnAsync(CancellationToken cancellationToken)
    {
        var existing = await _context.Students.Select(s => s.LRN).ToListAsync(cancellationToken);
        long next = 100100100001L;
        foreach (var l in existing)
        {
            if (long.TryParse(l, out var n) && n >= next) next = n + 1;
        }
        return next.ToString("D12");
    }
}
