using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.RequirementTemplates;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.RequirementTemplates.Queries;

public record GetRequirementTemplatesQuery() : IRequest<List<RequirementTemplateDto>>;

public class GetRequirementTemplatesQueryHandler : IRequestHandler<GetRequirementTemplatesQuery, List<RequirementTemplateDto>>
{
    private readonly IApplicationDbContext _context;

    public GetRequirementTemplatesQueryHandler(IApplicationDbContext context) => _context = context;

    public async Task<List<RequirementTemplateDto>> Handle(GetRequirementTemplatesQuery request, CancellationToken cancellationToken)
    {
        return await _context.RequirementTemplates
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.DocumentName)
            .Select(t => new RequirementTemplateDto(t.Id, t.DocumentName, t.GradeLevel, t.IsActive, t.DisplayOrder))
            .ToListAsync(cancellationToken);
    }
}
