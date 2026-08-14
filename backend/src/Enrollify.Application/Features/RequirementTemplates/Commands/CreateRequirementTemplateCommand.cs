using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.RequirementTemplates;
using Enrollify.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.RequirementTemplates.Commands;

public record CreateRequirementTemplateCommand(string DocumentName, string? GradeLevel, int DisplayOrder) : IRequest<RequirementTemplateDto>;

public class CreateRequirementTemplateCommandHandler : IRequestHandler<CreateRequirementTemplateCommand, RequirementTemplateDto>
{
    private readonly IApplicationDbContext _context;

    public CreateRequirementTemplateCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<RequirementTemplateDto> Handle(CreateRequirementTemplateCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentName))
            throw new ArgumentException("Document name is required.");

        var name = request.DocumentName.Trim();
        var grade = string.IsNullOrWhiteSpace(request.GradeLevel) ? null : request.GradeLevel.Trim();

        var duplicate = await _context.RequirementTemplates
            .AnyAsync(t => t.DocumentName == name && t.GradeLevel == grade, cancellationToken);
        if (duplicate)
            throw new InvalidOperationException($"A template named '{name}' already exists for this scope.");

        var template = new RequirementTemplate
        {
            DocumentName = name,
            GradeLevel = grade,
            IsActive = true,
            DisplayOrder = request.DisplayOrder
        };

        _context.RequirementTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        return new RequirementTemplateDto(template.Id, template.DocumentName, template.GradeLevel, template.IsActive, template.DisplayOrder);
    }
}
