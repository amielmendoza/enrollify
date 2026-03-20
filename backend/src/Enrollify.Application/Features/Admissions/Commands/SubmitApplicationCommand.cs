using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Admissions;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Enrollify.Application.Features.Admissions.Commands;

public record SubmitApplicationCommand(
    string FirstName, string? MiddleName, string LastName,
    string Email, string? ContactNumber, string Gender,
    DateTime DateOfBirth, string? Address,
    string GradeLevel, string SchoolYear,
    string? PreviousSchool, string? PreviousSchoolAddress,
    string? GuardianName, string? GuardianContact, string? GuardianRelationship,
    Guid TenantId
) : IRequest<ApplicationDetailDto>;

public class SubmitApplicationCommandValidator : AbstractValidator<SubmitApplicationCommand>
{
    public SubmitApplicationCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Gender).NotEmpty();
        RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(DateTime.UtcNow);
        RuleFor(x => x.GradeLevel).NotEmpty();
        RuleFor(x => x.SchoolYear).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
    }
}

public class SubmitApplicationCommandHandler : IRequestHandler<SubmitApplicationCommand, ApplicationDetailDto>
{
    private readonly IApplicationDbContext _context;

    public SubmitApplicationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationDetailDto> Handle(SubmitApplicationCommand request, CancellationToken cancellationToken)
    {
        var appNumber = $"APP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

        var application = new AdmissionApplication
        {
            ApplicationNumber = appNumber,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            Email = request.Email,
            ContactNumber = request.ContactNumber,
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            Address = request.Address,
            GradeLevel = request.GradeLevel,
            SchoolYear = request.SchoolYear,
            PreviousSchool = request.PreviousSchool,
            PreviousSchoolAddress = request.PreviousSchoolAddress,
            GuardianName = request.GuardianName,
            GuardianContact = request.GuardianContact,
            GuardianRelationship = request.GuardianRelationship,
            Status = "Submitted",
            TenantId = request.TenantId
        };

        _context.AdmissionApplications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);

        return ToDetailDto(application);
    }

    private static ApplicationDetailDto ToDetailDto(AdmissionApplication a) => new(
        a.Id, a.ApplicationNumber, a.FirstName, a.MiddleName, a.LastName,
        a.Email, a.ContactNumber, a.Gender, a.DateOfBirth, a.Address,
        a.GradeLevel, a.SchoolYear, a.PreviousSchool, a.PreviousSchoolAddress,
        a.GuardianName, a.GuardianContact, a.GuardianRelationship,
        a.Status, a.CreatedAt, a.ReviewedAt, a.ReviewNotes, a.StudentId);
}
