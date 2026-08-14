using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ApplicationFormFields;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ApplicationFormFields.Commands;

public record CreateApplicationFormFieldCommand(
    string FieldKey, string Label, string FieldType, string Section, string AppliesTo,
    bool IsRequired, bool IsVisible, int DisplayOrder, string? Options, string? HelpText
) : IRequest<ApplicationFormFieldDto>;

public class CreateApplicationFormFieldCommandValidator : AbstractValidator<CreateApplicationFormFieldCommand>
{
    private static readonly HashSet<string> ValidTypes = new() { "Text", "TextArea", "Number", "Date", "Checkbox", "Dropdown" };
    private static readonly HashSet<string> ValidSections = new() { "Parent", "Student", "Enrollment", "Guardian" };
    private static readonly HashSet<string> ValidApplies = new() { "Both", "ParentMode", "StudentMode" };

    public CreateApplicationFormFieldCommandValidator()
    {
        RuleFor(x => x.FieldKey).NotEmpty().MaximumLength(60)
            .Matches("^[a-z][a-zA-Z0-9_]*$").WithMessage("Field key must start with a lowercase letter and contain only letters, digits, and underscores.");
        RuleFor(x => x.Label).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FieldType).Must(v => ValidTypes.Contains(v)).WithMessage("Invalid field type.");
        RuleFor(x => x.Section).Must(v => ValidSections.Contains(v)).WithMessage("Invalid section.");
        RuleFor(x => x.AppliesTo).Must(v => ValidApplies.Contains(v)).WithMessage("Invalid 'AppliesTo'.");
        RuleFor(x => x.Options)
            .NotEmpty().When(x => x.FieldType == "Dropdown")
            .WithMessage("Dropdown fields must include at least one option.");
    }
}

public class CreateApplicationFormFieldCommandHandler : IRequestHandler<CreateApplicationFormFieldCommand, ApplicationFormFieldDto>
{
    private readonly IApplicationDbContext _context;

    public CreateApplicationFormFieldCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ApplicationFormFieldDto> Handle(CreateApplicationFormFieldCommand request, CancellationToken cancellationToken)
    {
        var keyExists = await _context.ApplicationFormFields
            .AnyAsync(f => f.FieldKey == request.FieldKey, cancellationToken);
        if (keyExists)
            throw new InvalidOperationException($"A field with key '{request.FieldKey}' already exists.");

        var field = new ApplicationFormField
        {
            FieldKey = request.FieldKey,
            Label = request.Label,
            FieldType = request.FieldType,
            Section = request.Section,
            AppliesTo = request.AppliesTo,
            IsRequired = request.IsRequired,
            IsVisible = request.IsVisible,
            IsBuiltIn = false,
            DisplayOrder = request.DisplayOrder,
            Options = request.Options,
            HelpText = request.HelpText
        };

        _context.ApplicationFormFields.Add(field);
        await _context.SaveChangesAsync(cancellationToken);

        return new ApplicationFormFieldDto(
            field.Id, field.FieldKey, field.Label, field.FieldType, field.Section, field.AppliesTo,
            field.IsRequired, field.IsVisible, field.IsBuiltIn, field.IsCore, field.DisplayOrder, field.Options, field.HelpText);
    }
}
