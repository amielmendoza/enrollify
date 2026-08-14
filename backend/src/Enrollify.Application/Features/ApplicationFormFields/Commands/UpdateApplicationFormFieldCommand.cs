using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.ApplicationFormFields;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.ApplicationFormFields.Commands;

public record UpdateApplicationFormFieldCommand(
    Guid Id,
    string FieldKey, string Label, string FieldType, string Section, string AppliesTo,
    bool IsRequired, bool IsVisible, int DisplayOrder, string? Options, string? HelpText
) : IRequest<ApplicationFormFieldDto>;

public class UpdateApplicationFormFieldCommandHandler : IRequestHandler<UpdateApplicationFormFieldCommand, ApplicationFormFieldDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateApplicationFormFieldCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<ApplicationFormFieldDto> Handle(UpdateApplicationFormFieldCommand request, CancellationToken cancellationToken)
    {
        var field = await _context.ApplicationFormFields
            .FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Field not found.");

        // Built-ins lock identity (key, type, section, appliesTo). Only label / required / visible /
        // order / options / helpText can be changed by the admin.
        if (!field.IsBuiltIn)
        {
            field.FieldKey = request.FieldKey;
            field.FieldType = request.FieldType;
            field.Section = request.Section;
            field.AppliesTo = request.AppliesTo;
        }

        field.Label = request.Label;
        field.DisplayOrder = request.DisplayOrder;
        field.HelpText = request.HelpText;

        // Core fields can never be hidden or made optional — the application would break.
        // The admin can rename them and reorder them, but visibility/requiredness are pinned on.
        if (field.IsCore)
        {
            field.IsRequired = true;
            field.IsVisible = true;
        }
        else
        {
            field.IsRequired = request.IsRequired;
            field.IsVisible = request.IsVisible;
            field.Options = request.Options;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new ApplicationFormFieldDto(
            field.Id, field.FieldKey, field.Label, field.FieldType, field.Section, field.AppliesTo,
            field.IsRequired, field.IsVisible, field.IsBuiltIn, field.IsCore, field.DisplayOrder, field.Options, field.HelpText);
    }
}
