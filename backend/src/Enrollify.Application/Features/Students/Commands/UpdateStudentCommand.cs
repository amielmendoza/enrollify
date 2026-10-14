using Enrollify.Application.Common;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Learners;
using Enrollify.Application.DTOs.Students;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Students.Commands;

public record UpdateStudentCommand(
    Guid Id, string? LRN, string FirstName, string MiddleName, string LastName,
    DateTime BirthDate, string? Gender, string Address,
    string? ContactNumber, string? Email, string? GuardianName, string? GuardianContact,
    LearnerProfileDto? Profile = null
) : IRequest<StudentDto>;

public class UpdateStudentCommandValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        // Shape is checked in the handler, and only when the LRN CHANGES — an untouched legacy
        // value must never block an unrelated edit.
        RuleFor(x => x.LRN).MaximumLength(20);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        // DELIBERATE data-quality ratchet on the STAFF path: a staff save needs either a
        // PSGC-matched address or (for legacy records not matched yet) the free-text line.
        // Do not relax this to match the public application form.
        RuleFor(x => x.Address).NotEmpty().When(x => !IsPsgcMatched(x.Profile))
            .WithMessage("Enter the address, or choose the city/municipality and barangay.")
            .MaximumLength(500);
        RuleFor(x => x.Profile!).SetValidator(new LearnerProfileDtoValidator()).When(x => x.Profile != null);
    }

    private static bool IsPsgcMatched(LearnerProfileDto? p) =>
        !string.IsNullOrWhiteSpace(p?.HomeAddress?.CityMunicipalityCode) && !string.IsNullOrWhiteSpace(p.HomeAddress.BarangayCode);
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

        var newLrn = string.IsNullOrWhiteSpace(request.LRN) ? null : request.LRN.Trim();
        if (newLrn != student.LRN)
        {
            if (newLrn != null && !DepEdLists.IsTwelveDigitLrn(newLrn))
                throw new InvalidOperationException("LRN must be exactly 12 digits (leave it blank if the learner has none yet).");
            if (newLrn != null && await _context.Students.AnyAsync(s => s.Id != student.Id && s.LRN == newLrn, cancellationToken))
                throw new InvalidOperationException($"A student with LRN '{newLrn}' already exists.");
            student.LRN = newLrn;
            student.LrnNeedsReview = false; // the registrar just set it deliberately
        }

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

        if (request.Profile != null)
            LearnerProfileMapping.Apply(request.Profile, student);

        await _context.SaveChangesAsync(cancellationToken);

        return StudentMapping.ToDto(student);
    }
}
