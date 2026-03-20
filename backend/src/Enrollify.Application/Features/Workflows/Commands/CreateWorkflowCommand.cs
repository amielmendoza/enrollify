using Enrollify.Application.Common.Interfaces;
using Enrollify.Application.DTOs.Workflows;
using Enrollify.Domain.Entities;
using FluentValidation;
using MediatR;

namespace Enrollify.Application.Features.Workflows.Commands;

public record CreateWorkflowCommand(
    string Name, string? Description, List<CreateWorkflowStepRequest> Steps
) : IRequest<WorkflowDefinitionDto>;

public class CreateWorkflowCommandValidator : AbstractValidator<CreateWorkflowCommand>
{
    public CreateWorkflowCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Steps).NotEmpty().WithMessage("At least one workflow step is required.");
    }
}

public class CreateWorkflowCommandHandler : IRequestHandler<CreateWorkflowCommand, WorkflowDefinitionDto>
{
    private readonly IApplicationDbContext _context;

    public CreateWorkflowCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<WorkflowDefinitionDto> Handle(CreateWorkflowCommand request, CancellationToken cancellationToken)
    {
        var workflow = new WorkflowDefinition
        {
            Name = request.Name,
            Description = request.Description
        };

        foreach (var step in request.Steps.OrderBy(s => s.StepOrder))
        {
            workflow.Steps.Add(new WorkflowStep
            {
                StepOrder = step.StepOrder,
                StepName = step.StepName,
                FromStatus = step.FromStatus,
                ToStatus = step.ToStatus,
                RequiredRole = step.RequiredRole,
                RequiresApproval = step.RequiresApproval
            });
        }

        _context.WorkflowDefinitions.Add(workflow);
        await _context.SaveChangesAsync(cancellationToken);

        return new WorkflowDefinitionDto(
            workflow.Id, workflow.Name, workflow.Description, workflow.IsActive,
            workflow.Steps.Select(s => new WorkflowStepDto(
                s.Id, s.StepOrder, s.StepName, s.FromStatus, s.ToStatus, s.RequiredRole, s.RequiresApproval
            )).ToList());
    }
}
