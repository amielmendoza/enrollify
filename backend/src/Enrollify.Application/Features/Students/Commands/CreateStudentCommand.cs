using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Learners;
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
    Guid? ParentUserId = null,  // Set for Parent-mode applications (parent owns this student)
    LearnerProfileDto? Profile = null
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
        // Staff-created learners must be PSGC-resolved down to the barangay (SF1 needs it);
        // the free-text line is now just house/street and may be blank (rural sitios).
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Profile)
            .Must(p => !string.IsNullOrWhiteSpace(p?.HomeAddress?.CityMunicipalityCode) && !string.IsNullOrWhiteSpace(p.HomeAddress.BarangayCode))
            .WithMessage("Choose the learner's city/municipality and barangay.");
        RuleFor(x => x.Profile!).SetValidator(new LearnerProfileDtoValidator()).When(x => x.Profile != null);
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
        if (request.Profile != null)
            LearnerProfileMapping.Apply(request.Profile, student);

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
