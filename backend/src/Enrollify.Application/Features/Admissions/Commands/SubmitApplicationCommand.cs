using System.Text.Json;
using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Admissions;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Admissions.Commands;

/// <summary>
/// Submits one or more admission applications in a single batch.
/// - Parent mode (anonymous): one parent context (name/email/contact) + N children → N applications.
/// - Parent mode (authenticated parent): N applications, all linked to the existing parent's UserId.
/// - Student mode: exactly one applicant (the student themselves), no parent context.
/// </summary>
public record SubmitApplicationCommand(
    Guid? AuthenticatedParentUserId,
    string ApplicationType,
    string? ParentFirstName, string? ParentLastName,
    string? ParentEmail, string? ParentContactNumber,
    List<SubmitApplicationCommand.Applicant> Applicants,
    Guid TenantId
) : IRequest<List<ApplicationDetailDto>>
{
    public record Applicant(
        string FirstName, string? MiddleName, string LastName,
        string Email, string? ContactNumber, string Gender,
        DateTime DateOfBirth, string? Address,
        string GradeLevel, string SchoolYear,
        string? PreviousSchool, string? PreviousSchoolAddress,
        string? GuardianName, string? GuardianContact, string? GuardianRelationship,
        Dictionary<string, string?>? CustomFieldValues);
}

public class SubmitApplicationCommandValidator : AbstractValidator<SubmitApplicationCommand>
{
    public SubmitApplicationCommandValidator()
    {
        RuleFor(x => x.ApplicationType).NotEmpty()
            .Must(v => v == "Parent" || v == "Student")
            .WithMessage("Application type must be 'Parent' or 'Student'.");
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Applicants).NotEmpty()
            .WithMessage("At least one applicant is required.");

        // Student mode must have exactly one applicant (the student themselves)
        When(x => x.ApplicationType == "Student", () =>
        {
            RuleFor(x => x.Applicants).Must(list => list.Count == 1)
                .WithMessage("Student-mode applications must have exactly one applicant.");
        });

        // Parent fields are required only for anonymous Parent-type submissions
        When(x => x.AuthenticatedParentUserId == null && x.ApplicationType == "Parent", () =>
        {
            RuleFor(x => x.ParentFirstName).NotEmpty().MaximumLength(100)
                .WithMessage("Parent first name is required.");
            RuleFor(x => x.ParentLastName).NotEmpty().MaximumLength(100)
                .WithMessage("Parent last name is required.");
            RuleFor(x => x.ParentEmail).NotEmpty().EmailAddress()
                .WithMessage("A valid parent email is required.");
        });

        // A Student-mode applicant signs in with their own email once approved, so it's required
        // there. Parent-mode children don't need one (the account is created from the parent's
        // email) — the form config marks it optional and the UI renders it that way.
        When(x => x.ApplicationType == "Student" && x.AuthenticatedParentUserId == null, () =>
        {
            RuleForEach(x => x.Applicants).ChildRules(a =>
                a.RuleFor(x => x.Email).NotEmpty().EmailAddress()
                    .WithMessage("A valid email is required for student applications — it becomes your login."));
        });

        RuleForEach(x => x.Applicants).ChildRules(a =>
        {
            a.RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
            a.RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
            a.RuleFor(x => x.Email).EmailAddress()
                .When(x => !string.IsNullOrWhiteSpace(x.Email))
                .WithMessage("'Email' is not a valid email address.");
            a.RuleFor(x => x.Gender).NotEmpty();
            a.RuleFor(x => x.DateOfBirth).NotEmpty().LessThan(DateTime.UtcNow);
            a.RuleFor(x => x.GradeLevel).NotEmpty();
            a.RuleFor(x => x.SchoolYear).NotEmpty();
        });
    }
}

public class SubmitApplicationCommandHandler : IRequestHandler<SubmitApplicationCommand, List<ApplicationDetailDto>>
{
    private readonly IApplicationDbContext _context;

    public SubmitApplicationCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ApplicationDetailDto>> Handle(SubmitApplicationCommand request, CancellationToken cancellationToken)
    {
        // Authenticated parent → always Parent application linked to existing parent
        var effectiveType = request.AuthenticatedParentUserId != null ? "Parent" : request.ApplicationType;
        var normalizedParentEmail = effectiveType == "Parent"
            ? request.ParentEmail?.Trim().ToLowerInvariant()
            : null;

        // Load the field config for this tenant so we can validate required custom fields and
        // strip any keys the admin doesn't actually have configured.
        var customFields = await _context.ApplicationFormFields.IgnoreQueryFilters()
            .Where(f => f.TenantId == request.TenantId && f.IsVisible && !f.IsBuiltIn)
            .ToListAsync(cancellationToken);

        var created = new List<AdmissionApplication>();

        foreach (var applicant in request.Applicants)
        {
            // Validate required custom fields and trim values to known keys
            var sanitizedCustomValues = new Dictionary<string, string?>();
            foreach (var field in customFields)
            {
                if (field.AppliesTo == "ParentMode" && effectiveType != "Parent") continue;
                if (field.AppliesTo == "StudentMode" && effectiveType != "Student") continue;

                var raw = applicant.CustomFieldValues != null && applicant.CustomFieldValues.TryGetValue(field.FieldKey, out var v) ? v : null;
                var trimmed = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

                if (field.IsRequired && string.IsNullOrEmpty(trimmed))
                    throw new InvalidOperationException($"'{field.Label}' is required.");

                sanitizedCustomValues[field.FieldKey] = trimmed;
            }

            var customJson = sanitizedCustomValues.Count > 0
                ? JsonSerializer.Serialize(sanitizedCustomValues)
                : null;

            var appNumber = $"APP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            var application = new AdmissionApplication
            {
                ApplicationNumber = appNumber,
                FirstName = applicant.FirstName,
                MiddleName = applicant.MiddleName,
                LastName = applicant.LastName,
                Email = applicant.Email,
                ContactNumber = applicant.ContactNumber,
                Gender = applicant.Gender,
                DateOfBirth = applicant.DateOfBirth,
                Address = applicant.Address,
                GradeLevel = applicant.GradeLevel,
                SchoolYear = applicant.SchoolYear,
                PreviousSchool = applicant.PreviousSchool,
                PreviousSchoolAddress = applicant.PreviousSchoolAddress,
                GuardianName = applicant.GuardianName,
                GuardianContact = applicant.GuardianContact,
                GuardianRelationship = applicant.GuardianRelationship,
                Status = "Submitted",
                TenantId = request.TenantId,
                ApplicationType = effectiveType,
                ParentUserId = request.AuthenticatedParentUserId,
                ParentFirstName = effectiveType == "Parent" ? request.ParentFirstName : null,
                ParentLastName = effectiveType == "Parent" ? request.ParentLastName : null,
                ParentEmail = normalizedParentEmail,
                ParentContactNumber = effectiveType == "Parent" ? request.ParentContactNumber : null,
                CustomFieldValues = customJson
            };

            _context.AdmissionApplications.Add(application);
            created.Add(application);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return created.Select(ToDetailDto).ToList();
    }

    private static ApplicationDetailDto ToDetailDto(AdmissionApplication a) => new(
        a.Id, a.ApplicationNumber, a.FirstName, a.MiddleName, a.LastName,
        a.Email, a.ContactNumber, a.Gender, a.DateOfBirth, a.Address,
        a.GradeLevel, a.SchoolYear, a.PreviousSchool, a.PreviousSchoolAddress,
        a.GuardianName, a.GuardianContact, a.GuardianRelationship,
        a.Status, a.CreatedAt, a.ReviewedAt, a.ReviewNotes, a.StudentId,
        a.ApplicationType, a.ParentFirstName, a.ParentLastName, a.ParentEmail, a.ParentContactNumber,
        ParseCustomFieldValues(a.CustomFieldValues));

    private static Dictionary<string, string?>? ParseCustomFieldValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, string?>>(json); }
        catch { return null; }
    }
}
