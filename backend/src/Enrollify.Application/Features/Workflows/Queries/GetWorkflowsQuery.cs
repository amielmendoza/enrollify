using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Workflows;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Enrollify.Application.Features.Workflows.Queries;

public record GetWorkflowsQuery() : IRequest<List<WorkflowDefinitionDto>>;

public class GetWorkflowsQueryHandler : IRequestHandler<GetWorkflowsQuery, List<WorkflowDefinitionDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWorkflowsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WorkflowDefinitionDto>> Handle(GetWorkflowsQuery request, CancellationToken cancellationToken)
    {
        return await _context.WorkflowDefinitions
            .Include(w => w.Steps.OrderBy(s => s.StepOrder))
            .OrderBy(w => w.Name)
            .Select(w => new WorkflowDefinitionDto(
                w.Id, w.Name, w.Description, w.IsActive,
                w.Steps.Select(s => new WorkflowStepDto(
                    s.Id, s.StepOrder, s.StepName, s.FromStatus, s.ToStatus, s.RequiredRole, s.RequiresApproval
                )).ToList()))
            .ToListAsync(cancellationToken);
    }
}
