using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.RequirementTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.RequirementTemplates.Commands;

public record UpdateRequirementTemplateCommand(Guid Id, string DocumentName, string? GradeLevel, bool IsActive, int DisplayOrder) : IRequest<RequirementTemplateDto>;

public class UpdateRequirementTemplateCommandHandler : IRequestHandler<UpdateRequirementTemplateCommand, RequirementTemplateDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateRequirementTemplateCommandHandler(IApplicationDbContext context) => _context = context;

    public async Task<RequirementTemplateDto> Handle(UpdateRequirementTemplateCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentName))
            throw new ArgumentException("Document name is required.");

        var template = await _context.RequirementTemplates
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Template not found.");

        template.DocumentName = request.DocumentName.Trim();
        template.GradeLevel = string.IsNullOrWhiteSpace(request.GradeLevel) ? null : request.GradeLevel.Trim();
        template.IsActive = request.IsActive;
        template.DisplayOrder = request.DisplayOrder;

        await _context.SaveChangesAsync(cancellationToken);

        return new RequirementTemplateDto(template.Id, template.DocumentName, template.GradeLevel, template.IsActive, template.DisplayOrder);
    }
}
