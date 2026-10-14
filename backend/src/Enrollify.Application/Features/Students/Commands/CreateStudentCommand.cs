using Enrollify.Application.Common;
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
        RuleFor(x => x.LRN)
            .Must(l => DepEdLists.IsTwelveDigitLrn(l!.Trim()))
            .When(x => !string.IsNullOrWhiteSpace(x.LRN))
            .WithMessage("LRN must be exactly 12 digits (leave it blank if the learner has none yet).");
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
        if (lrn != null && await _context.Students.AnyAsync(s => s.LRN == lrn, cancellationToken))
            throw new InvalidOperationException($"A student with LRN '{lrn}' already exists.");

        var student = new Student
        {
            StudentNumber = await StudentNumbering.NextStudentNumberAsync(_context, DateTime.UtcNow, cancellationToken),
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

        // Two concurrent creates can mint the same student number; the (TenantId, StudentNumber)
        // unique index rejects the loser — regenerate and retry a couple of times.
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                break;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                student.StudentNumber = await StudentNumbering.NextStudentNumberAsync(_context, DateTime.UtcNow, cancellationToken);
            }
        }

        return StudentMapping.ToDto(student);
    }
}
